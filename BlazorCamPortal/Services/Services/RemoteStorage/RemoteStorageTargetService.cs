using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.RemoteStorageDtos;
using CamPortal.Contracts.Models;
using CamPortal.Core.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;

namespace CamPortal.Core.Services.RemoteStorage
{
    public class RemoteStorageTargetService : IRemoteStorageService
    {
        private readonly IRemoteStorageTargetRepository _remoteStorageTargetRepository;
        private readonly ISmbConnectionService _smbConnectionService;
        private readonly ILogger<RemoteStorageTargetService> _logger;
        private readonly TimeSpan _verificationTimeout;

        public RemoteStorageTargetService(
            IRemoteStorageTargetRepository remoteStorageTargetRepository,
            ISmbConnectionService smbConnectionService,
            IConfiguration configuration,
            ILogger<RemoteStorageTargetService> logger)
        {
            _remoteStorageTargetRepository = remoteStorageTargetRepository;
            _smbConnectionService = smbConnectionService;
            _logger = logger;

            _verificationTimeout = TimeSpan.FromSeconds(int.Parse(
                configuration.GetSection("RemoteStorageConfig")["VerificationTimeoutInSeconds"]
                ?? throw new ArgumentNullException("VerificationTimeoutInSeconds not configured")));
        }

        public async Task<List<RemoteStorageTargetModel>> GetTargetsAsync()
        {
            var targets = await _remoteStorageTargetRepository.GetTargetsAsync();

            return targets
                .Select(x => new RemoteStorageTargetModel
                {
                    Id = x.Id,
                    SharePath = x.SharePath,
                    Username = x.Username,
                    HasStoredPassword = !string.IsNullOrEmpty(x.ProtectedPassword),
                    IsSyncEnabled = x.IsSyncEnabled,
                    Retention = x.Retention
                })
                .ToList();
        }

        public async Task<RemoteStorageTargetDto?> GetEnabledTargetAsync(Guid remoteTargetId)
        {
            var target = await _remoteStorageTargetRepository.GetTargetAsync(remoteTargetId);

            return target;
        }

        public async Task<RemoteStorageOperationResultDto> SaveTargetAsync(RemoteStorageTargetModel target)
        {
            if (!ValidateSharePath(target.SharePath, out string? message))
            {
                return CreateResult(false, message ?? "Could not validate share path");
            }

            if (!MiscUtilities.ValidateModel(target, out ICollection<ValidationResult> results))
            {
                return CreateResult(false, string.Join(", ", results.Select(x => x.ErrorMessage)));
            }

            try
            {
                var targetDto = new RemoteStorageTargetDto
                {
                    Id = target.Id ?? Guid.NewGuid(),
                    SharePath = NormalizeSharePath(target.SharePath),
                    Username = target.Username.Trim(),
                    ProtectedPassword = string.IsNullOrEmpty(target.Password)
                        ? string.Empty
                        : _smbConnectionService.ProtectPassword(target.Password),
                    IsSyncEnabled = target.IsSyncEnabled,
                    Retention = target.Retention
                };

                if (await _remoteStorageTargetRepository.DoesSharePathExistAsync(targetDto.SharePath, targetDto.Id))
                {
                    return CreateResult(false, "SMB Share path already saved. Use another unused path.");
                }

                if (target.Id == null)
                {
                    await _remoteStorageTargetRepository.SaveTargetAsync(targetDto);
                    return CreateResult(true, "Remote storage settings saved.", targetDto.Id);
                }

                await _remoteStorageTargetRepository.UpdateTargetAsync(targetDto);

                return CreateResult(true, "Remote storage settings updated.", targetDto.Id);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to save remote storage targets.");

                return CreateResult(false, "Failed to save remote storage settings.");
            }
        }

        public async Task<RemoteStorageOperationResultDto> VerifyTargetAsync(RemoteStorageTargetModel target)
        {
            if (!ValidateSharePath(target.SharePath, out string? validationError))
            {
                return CreateResult(false, validationError ?? "could not validate target");
            }

            var sharePath = NormalizeSharePath(target.SharePath);

            try
            {
                var password = await ResolvePasswordAsync(target);
                var username = target.Username.Trim();

                await Task.Run(() => _smbConnectionService.VerifyWriteAccess(sharePath, username, password))
                    .WaitAsync(_verificationTimeout);

                return CreateResult(true, $"Connected to {sharePath} and verified write access.");
            }
            catch (TimeoutException)
            {
                return CreateResult(false, $"{sharePath} did not respond within {_verificationTimeout.TotalSeconds} seconds.");
            }
            catch (CryptographicException exception)
            {
                _logger.LogWarning(exception, "Stored password for remote storage {SharePath} could not be decrypted.", sharePath);

                return CreateResult(false, "The stored password can no longer be decrypted. Enter the password again.");
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Verification of remote storage {SharePath} failed.", sharePath);

                return CreateResult(false, exception.Message);
            }
        }

        private async Task<string> ResolvePasswordAsync(RemoteStorageTargetModel target)
        {
            if (!string.IsNullOrEmpty(target.Password) || target.Id == null)
            {
                return target.Password;
            }

            var storedTarget = await _remoteStorageTargetRepository.GetTargetAsync(target.Id.Value);

            return storedTarget == null
                ? string.Empty
                : _smbConnectionService.UnprotectPassword(storedTarget.ProtectedPassword);
        }

        private bool ValidateSharePath(string sharePath, out string? message)
        {
            message = null;

            if (string.IsNullOrWhiteSpace(sharePath))
            {
                message = "Share path is required.";

                return false;
            }

            if (!SmbSharePathUtilities.TryParse(sharePath, out _))
            {
                message = $@"{sharePath} is not a valid share path. Use the format \\server\share.";

                return false;
            }

            return true;
        }

        private string NormalizeSharePath(string sharePath)
        {
            return SmbSharePathUtilities.TryParse(sharePath, out var smbSharePath)
                ? smbSharePath.ToString()
                : sharePath.Trim();
        }

        private RemoteStorageOperationResultDto CreateResult(bool succeeded, string message, Guid? targetId = null)
        {
            return new RemoteStorageOperationResultDto
            {
                TargetId = targetId,
                Succeeded = succeeded,
                Message = message
            };
        }
    }
}
