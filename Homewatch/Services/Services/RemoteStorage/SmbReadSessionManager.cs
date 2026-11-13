using Homewatch.Contracts.Abstractions.Services;
using Homewatch.Contracts.Dtos.RemoteStorageDtos;
using System.Collections.Concurrent;

namespace Homewatch.Core.Services.RemoteStorage
{
    public class SmbReadSessionManager : ISmbReadSessionManager, IDisposable
    {
        private const int MaxSessionsPerTarget = 4;

        private static readonly TimeSpan _idleSessionTimeout = TimeSpan.FromMinutes(5);

        private readonly ISmbConnectionService _smbConnectionService;
        private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _sessionLimits = new();
        private readonly ConcurrentDictionary<Guid, ConcurrentQueue<(ISmbShareSession Session, string ConnectionKey, DateTime LastUsedUtc)>> _idleSessions = new();

        public SmbReadSessionManager(ISmbConnectionService smbConnectionService)
        {
            _smbConnectionService = smbConnectionService;
        }

        public async Task<byte[]?> ReadFileAsync(RemoteStorageTargetDto target, string relativePath, CancellationToken cancellationToken)
        {
            var sessionLimit = _sessionLimits.GetOrAdd(target.Id, _ => new SemaphoreSlim(MaxSessionsPerTarget, MaxSessionsPerTarget));

            await sessionLimit.WaitAsync(cancellationToken);

            try
            {
                return await Task.Run(() => ReadWithPooledSession(target, relativePath), cancellationToken);
            }
            finally
            {
                sessionLimit.Release();
            }
        }

        public void Dispose()
        {
            foreach (var idleSessions in _idleSessions.Values)
            {
                while (idleSessions.TryDequeue(out var idleSession))
                {
                    idleSession.Session.Dispose();
                }
            }
        }

        private byte[]? ReadWithPooledSession(RemoteStorageTargetDto target, string relativePath)
        {
            var connectionKey = $"{target.SharePath}|{target.Username}|{target.ProtectedPassword}";
            var idleSessions = _idleSessions.GetOrAdd(target.Id, _ => new());
            var session = TakeIdleSession(idleSessions, connectionKey) ?? _smbConnectionService.OpenSession(target);

            try
            {
                var content = session.ReadAllBytes(relativePath);

                idleSessions.Enqueue((session, connectionKey, DateTime.UtcNow));

                return content;
            }
            catch
            {
                session.Dispose();
                throw;
            }
        }

        private ISmbShareSession? TakeIdleSession(
            ConcurrentQueue<(ISmbShareSession Session, string ConnectionKey, DateTime LastUsedUtc)> idleSessions,
            string connectionKey)
        {
            while (idleSessions.TryDequeue(out var idleSession))
            {
                if (idleSession.ConnectionKey == connectionKey
                    && idleSession.Session.IsConnected
                    && DateTime.UtcNow - idleSession.LastUsedUtc < _idleSessionTimeout)
                {
                    return idleSession.Session;
                }

                idleSession.Session.Dispose();
            }

            return null;
        }
    }
}
