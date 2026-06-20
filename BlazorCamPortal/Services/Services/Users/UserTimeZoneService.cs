using CamPortal.Contracts.Abstractions.Services;
using Microsoft.JSInterop;

namespace CamPortal.Core.Services.Users
{
    public sealed class UserTimeZoneService : IUserTimeZoneService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly SemaphoreSlim _initLock = new(1, 1);

        private TimeZoneInfo _timeZone = TimeZoneInfo.Utc;
        private bool _isInitialized;

        public UserTimeZoneService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task EnsureInitializedAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            await _initLock.WaitAsync();

            try
            {
                if (_isInitialized)
                {
                    return;
                }

                var timeZoneId = await _jsRuntime.InvokeAsync<string>("getBrowserTimeZone");

                _timeZone = ResolveTimeZone(timeZoneId);
                _isInitialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        public DateTime ToLocal(DateTime utcDateTime)
        {
            var utc = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);

            return TimeZoneInfo.ConvertTimeFromUtc(utc, _timeZone);
        }

        public DateTime ToUtc(DateTime localDateTime)
        {
            var unspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);

            return TimeZoneInfo.ConvertTimeToUtc(unspecified, _timeZone);
        }

        private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
        {
            if (string.IsNullOrWhiteSpace(timeZoneId))
            {
                return TimeZoneInfo.Local;
            }

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch
            {
                return TimeZoneInfo.Local;
            }
        }
    }
}
