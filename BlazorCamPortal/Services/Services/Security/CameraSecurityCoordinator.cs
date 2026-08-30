using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.SecurityDtos;
using CamPortal.Contracts.Dtos.TelemetryDtos;
using CamPortal.Contracts.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace CamPortal.Core.Services.Security
{
    public class CameraSecurityCoordinator :
        ICameraSecurityCoordinator,
        ICameraSecurityService,
        ICameraLiveTelemetry,
        ICameraStatusNotifier
    {
        private static readonly TimeSpan _notifyThrottle = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan _resetGrace = TimeSpan.FromSeconds(3);

        private readonly ILogger<CameraSecurityCoordinator> _logger;
        private readonly ICameraConfigurationRepository _deviceRepository;
        private readonly ISystemSettingsService _systemSettingsService;
        private readonly IServiceProvider _serviceProvider;

        private readonly ConcurrentDictionary<Guid, CameraState> _states = new();
        private readonly HashSet<Guid> _camerasInAlarm = new();
        private readonly object _alarmLock = new();

        private readonly SemaphoreSlim _thresholdsLock = new(1, 1);
        private volatile bool _thresholdsLoaded;
        private volatile SecurityThresholds _thresholds = new(4, 60, 80);

        private ICameraCommandDispatcher? _cameraCommandDispatcher;
        private ICameraCommandDispatcher _dispatcher => _cameraCommandDispatcher ??= _serviceProvider.GetRequiredService<ICameraCommandDispatcher>();

        public event Action<Guid>? StatusChanged;

        public CameraSecurityCoordinator(
            ILogger<CameraSecurityCoordinator> logger,
            ICameraConfigurationRepository deviceRepository,
            ISystemSettingsService systemSettingsService,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _deviceRepository = deviceRepository;
            _systemSettingsService = systemSettingsService;
            _serviceProvider = serviceProvider;
        }

        public async Task OnCameraConnectedAsync(Guid cameraId)
        {
            await EnsureThresholdsLoadedAsync();

            var config = await _deviceRepository.GetDeviceSecurityConfigAsync(cameraId);

            var state = _states.GetOrAdd(cameraId, _ => new CameraState());

            lock (state.Sync)
            {
                state.Armed = config?.SecurityArmed ?? false;
                state.CaseSensorInstalled = config?.CaseSensorInstalled ?? true;
                state.MovementThresholdOffset = config?.MovementThresholdOffset ?? 0;
                state.RotationThresholdOffset = config?.RotationThresholdOffset ?? 0;
                state.Connected = true;
            }

            PushConfigToDevice(cameraId, state);

            bool siteAlarmActive;
            lock (_alarmLock)
            {
                siteAlarmActive = _camerasInAlarm.Count > 0;
            }

            if (siteAlarmActive)
            {
                _dispatcher.TryEnqueueCommand(cameraId, DeviceCommand.TriggerSecurityAlarm);
            }

            NotifyStatusChanged(cameraId);
        }

        public void OnCameraDisconnected(Guid cameraId)
        {
            if (!_states.TryGetValue(cameraId, out var state))
            {
                return;
            }

            bool wasAlarm;
            lock (state.Sync)
            {
                state.Connected = false;
                wasAlarm = state.AlarmActive;
                state.AlarmActive = false;
                state.Warning = false;
            }

            if (wasAlarm)
            {
                RemoveFromAlarmSet(cameraId);
            }

            NotifyStatusChanged(cameraId);
        }

        public void Ingest(CameraTelemetrySampleDto sample)
        {
            var state = _states.GetOrAdd(sample.CameraId, _ => new CameraState());

            bool becameAlarm;
            bool notify;

            lock (state.Sync)
            {
                state.Latest = sample;
                state.Connected = true;

                var thresholds = _thresholds;
                bool warning = false;

                List<string> cameraSecurityProblems = new();

                if (sample.Fps < thresholds.MinFps)
                {
                    warning = true;
                    cameraSecurityProblems.Add($"Low FPS ({sample.Fps:0.0})");
                }

                if (sample.TempHumiditySensorPresent && sample.TemperatureC > thresholds.MaxTemperatureC)
                {
                    warning = true;
                    cameraSecurityProblems.Add($"High temperature ({sample.TemperatureC:0.0} C)");
                }

                if (sample.TempHumiditySensorPresent && sample.HumidityPercent > thresholds.MaxHumidityPercent)
                {
                    warning = true;
                    cameraSecurityProblems.Add($"High humidity ({sample.HumidityPercent:0} %)");
                }

                var previousWarning = state.Warning;
                state.Warning = warning;
                state.WarningReason = string.Join(". ", cameraSecurityProblems);

                var wasAlarm = state.AlarmActive;

                var caseTamper = state.CaseSensorInstalled && sample.CaseOpen;
                var motionTamper = sample.MotionEvents != CameraMotionEvents.None || sample.MotionActive;
                var anyTamper = caseTamper || motionTamper;

                if (state.AwaitingClear && !anyTamper)
                {
                    state.AwaitingClear = false;
                }

                var suppressed = state.AwaitingClear || DateTime.UtcNow < state.SuppressTamperUntil;

                if (state.Armed && !suppressed && anyTamper)
                {
                    state.AlarmActive = true;
                }

                becameAlarm = !wasAlarm && state.AlarmActive;
                var stateChanged = becameAlarm || previousWarning != warning;

                var now = DateTime.UtcNow;
                if (stateChanged || now - state.LastNotifyUtc >= _notifyThrottle)
                {
                    state.LastNotifyUtc = now;
                    notify = true;
                }
                else
                {
                    notify = false;
                }
            }

            if (becameAlarm)
            {
                AddToAlarmSet(sample.CameraId);
            }

            if (notify)
            {
                NotifyStatusChanged(sample.CameraId);
            }
        }

        public Task ArmAsync(Guid cameraId, bool armed)
        {
            _dispatcher.TryEnqueueCommand(cameraId, DeviceCommand.ResetSecurityAlarm);

            return armed ? ArmInternalAsync(cameraId) : DisarmInternalAsync(cameraId);
        }

        private async Task ArmInternalAsync(Guid cameraId)
        {
            await _deviceRepository.SetSecurityArmedAsync(cameraId, true);

            var state = _states.GetOrAdd(cameraId, _ => new CameraState());
            lock (state.Sync)
            {
                state.Armed = true;
                state.AlarmActive = false;
                state.AwaitingClear = true;
                state.SuppressTamperUntil = DateTime.UtcNow + _resetGrace;
            }

            SyncDeviceToClearBaseline(cameraId);

            NotifyStatusChanged(cameraId);
        }

        private async Task DisarmInternalAsync(Guid cameraId)
        {
            await _deviceRepository.SetSecurityArmedAsync(cameraId, false);

            var state = _states.GetOrAdd(cameraId, _ => new CameraState());

            bool wasAlarm;
            lock (state.Sync)
            {
                state.Armed = false;
                wasAlarm = state.AlarmActive;
                state.AlarmActive = false;
            }

            if (wasAlarm)
            {
                ClearDeviceAlarm(cameraId);
            }

            NotifyStatusChanged(cameraId);
        }

        public async Task SetCaseSensorInstalledAsync(Guid cameraId, bool installed)
        {
            await _deviceRepository.SetCaseSensorInstalledAsync(cameraId, installed);

            var state = _states.GetOrAdd(cameraId, _ => new CameraState());
            lock (state.Sync)
            {
                state.CaseSensorInstalled = installed;
            }

            PushConfigToDevice(cameraId, state);

            NotifyStatusChanged(cameraId);
        }

        public async Task SetMovementThresholdsAsync(Guid cameraId, double movementThresholdOffset, double rotationThresholdOffset)
        {
            if (movementThresholdOffset < 0)
            {
                movementThresholdOffset = 0;
            }

            if (rotationThresholdOffset < 0)
            {
                rotationThresholdOffset = 0;
            }

            await _deviceRepository.SetMovementThresholdsAsync(cameraId, movementThresholdOffset, rotationThresholdOffset);

            var state = _states.GetOrAdd(cameraId, _ => new CameraState());
            lock (state.Sync)
            {
                state.MovementThresholdOffset = movementThresholdOffset;
                state.RotationThresholdOffset = rotationThresholdOffset;
            }

            PushConfigToDevice(cameraId, state);

            NotifyStatusChanged(cameraId);
        }

        private void PushConfigToDevice(Guid cameraId, CameraState state)
        {
            List<DeviceEspConfigDto> config;
            lock (state.Sync)
            {
                config =
                [
                     new DeviceEspConfigDto()
                     {
                         ConfigurationPropertyName=nameof(CameraOnboardEditableParameters.CaseSensorInstalled),
                         Value=state.CaseSensorInstalled
                     },
                     new DeviceEspConfigDto()
                     {
                         ConfigurationPropertyName=nameof(CameraOnboardEditableParameters.MovementThresholdOffset),
                         Value=state.MovementThresholdOffset
                     },
                     new DeviceEspConfigDto()
                     {
                         ConfigurationPropertyName=nameof(CameraOnboardEditableParameters.RotationThresholdOffset),
                         Value=state.RotationThresholdOffset
                     }
                ];
            }

            _dispatcher.TryEnqueueConfig(cameraId, config);
        }

        public Task TriggerAsync(Guid cameraId)
        {
            var state = _states.GetOrAdd(cameraId, _ => new CameraState());

            lock (state.Sync)
            {
                state.AlarmActive = true;
            }

            AddToAlarmSet(cameraId);

            NotifyStatusChanged(cameraId);
            return Task.CompletedTask;
        }

        public Task ResetAsync(Guid cameraId)
        {
            if (_states.TryGetValue(cameraId, out var state))
            {
                lock (state.Sync)
                {
                    state.AlarmActive = false;
                    state.AwaitingClear = true;
                    state.SuppressTamperUntil = DateTime.UtcNow + _resetGrace;
                }
            }

            ClearDeviceAlarm(cameraId);

            NotifyStatusChanged(cameraId);
            return Task.CompletedTask;
        }

        public async Task RefreshThresholdsAsync()
        {
            await LoadThresholdsAsync();
            _thresholdsLoaded = true;
        }

        public CameraTelemetrySampleDto? GetLatest(Guid cameraId)
        {
            return _states.TryGetValue(cameraId, out var state) ? state.Latest : null;
        }

        public CameraLiveStatusDto GetLiveStatus(Guid cameraId)
        {
            var status = new CameraLiveStatusDto { CameraId = cameraId };

            if (_states.TryGetValue(cameraId, out var state))
            {
                lock (state.Sync)
                {
                    status.Online = state.Connected;
                    status.SecurityArmed = state.Armed;
                    status.AlarmActive = state.AlarmActive;
                    status.Warning = state.Warning;
                    status.WarningReason = state.WarningReason;
                }
            }

            return status;
        }

        public CameraSecurityStatusDto GetStatus(Guid cameraId)
        {
            var status = new CameraSecurityStatusDto { CameraId = cameraId, CaseSensorInstalled = true };

            if (_states.TryGetValue(cameraId, out var state))
            {
                lock (state.Sync)
                {
                    status.Online = state.Connected;
                    status.SecurityArmed = state.Armed;
                    status.CaseSensorInstalled = state.CaseSensorInstalled;
                    status.AlarmActive = state.AlarmActive;
                    status.MovementThresholdOffset = state.MovementThresholdOffset;
                    status.RotationThresholdOffset = state.RotationThresholdOffset;

                    var latest = state.Latest;
                    if (latest != null)
                    {
                        status.HasTelemetry = true;
                        status.CaseOpen = latest.CaseOpen;
                        status.MotionActive = latest.MotionActive;
                        status.MotionEvents = latest.MotionEvents;
                        status.TempHumiditySensorPresent = latest.TempHumiditySensorPresent;
                        status.MotionSensorPresent = latest.MotionSensorPresent;
                        status.TemperatureC = latest.TemperatureC;
                        status.HumidityPercent = latest.HumidityPercent;
                        status.DewPointC = latest.DewPointC;
                    }
                }
            }

            return status;
        }

        public void NotifyStatusChanged(Guid cameraId)
        {
            var handlers = StatusChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList().Cast<Action<Guid>>())
            {
                try
                {
                    handler(cameraId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "StatusChanged handler threw for camera {CameraId}", cameraId);
                }
            }
        }

        private void AddToAlarmSet(Guid cameraId)
        {
            bool firstAlarm;
            lock (_alarmLock)
            {
                if (!_camerasInAlarm.Add(cameraId))
                {
                    return;
                }

                firstAlarm = _camerasInAlarm.Count == 1;
            }

            if (firstAlarm)
            {
                BroadcastToConnected(DeviceCommand.TriggerSecurityAlarm);
            }
        }

        private void RemoveFromAlarmSet(Guid cameraId)
        {
            bool lastCleared;
            lock (_alarmLock)
            {
                if (!_camerasInAlarm.Remove(cameraId))
                {
                    return;
                }

                lastCleared = _camerasInAlarm.Count == 0;
            }

            if (lastCleared)
            {
                BroadcastToConnected(DeviceCommand.ResetSecurityAlarm);
            }
        }

        private void SyncDeviceToClearBaseline(Guid cameraId)
        {
            _dispatcher.TryEnqueueCommand(cameraId, DeviceCommand.ResetSecurityAlarm);

            bool siteActive;
            lock (_alarmLock)
            {
                siteActive = _camerasInAlarm.Count > 0;
            }

            if (siteActive)
            {
                _dispatcher.TryEnqueueCommand(cameraId, DeviceCommand.TriggerSecurityAlarm);
            }
        }

        private void ClearDeviceAlarm(Guid cameraId)
        {
            _dispatcher.TryEnqueueCommand(cameraId, DeviceCommand.ResetSecurityAlarm);

            bool siteStillActive;
            lock (_alarmLock)
            {
                _camerasInAlarm.Remove(cameraId);
                siteStillActive = _camerasInAlarm.Count > 0;
            }

            if (siteStillActive)
            {
                _dispatcher.TryEnqueueCommand(cameraId, DeviceCommand.TriggerSecurityAlarm);
            }
            else
            {
                BroadcastToConnected(DeviceCommand.ResetSecurityAlarm);
            }
        }

        private void BroadcastToConnected(DeviceCommand command)
        {
            foreach (var entry in _states)
            {
                if (entry.Value.Connected)
                {
                    _dispatcher.TryEnqueueCommand(entry.Key, command);
                }
            }
        }

        private async Task EnsureThresholdsLoadedAsync()
        {
            if (_thresholdsLoaded)
            {
                return;
            }

            await _thresholdsLock.WaitAsync();
            try
            {
                if (_thresholdsLoaded)
                {
                    return;
                }

                await LoadThresholdsAsync();
                _thresholdsLoaded = true;
            }
            finally
            {
                _thresholdsLock.Release();
            }
        }

        private async Task LoadThresholdsAsync()
        {
            var settings = await _systemSettingsService.GetSystemSettingsAsync();
            _thresholds = new SecurityThresholds(
                settings.SecurityMinFps,
                settings.SecurityMaxTemperatureC,
                settings.SecurityMaxHumidityPercent);
        }
    }
}
