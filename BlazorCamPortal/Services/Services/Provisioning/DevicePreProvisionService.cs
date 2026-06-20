using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Abstractions.UnitOfWork;
using CamPortal.Contracts.Dtos.CameraDtos;
using CamPortal.Contracts.Dtos.DeviceDtos;
using CamPortal.Contracts.Dtos.LocalNetworkDtos;
using CamPortal.Contracts.Dtos.PreprovisionAttemptDtos;
using CamPortal.Contracts.Enums;
using CamPortal.Contracts.Models;
using CamPortal.Core.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Buffers.Text;
using System.Net;
using System.Text;
using System.Text.Json;

namespace CamPortal.Core.Services.Provisioning
{
    public class DevicePreProvisionService : IDevicePreProvisionService
    {
        private readonly int _gracePeriodForAllowedDeviceDeletionMinutes = 5;

        private readonly IDeviceAuthenticatorService _deviceAuthenticatorService;
        private readonly IDeviceService _deviceService;
        private readonly IDeviceRepository _deviceRepository;
        private readonly IPreprovisionAttemptRepository _preprovisionAttemptRepository;
        private readonly ILogger<DevicePreProvisionService> _logger;
        private readonly IUnitOfWorkFactory _unitOfWorkFactory;
        private readonly IPreprovisionNotifier _preprovisionNotifier;
        private readonly IServerIdentityService _serverIdentityService;

        private readonly int _preprovisionAttemptExpirationMinutes;
        private readonly string _deviceProvisioningUrl;

        public DevicePreProvisionService(
            IDeviceAuthenticatorService deviceAuthenticatorService,
            IDeviceService cameraService,
            IPreprovisionAttemptRepository preprovisionAttemptRepository,
            IDeviceRepository deviceRepository,
            ILogger<DevicePreProvisionService> logger,
            IUnitOfWorkFactory unitOfWorkFactory,
            IPreprovisionNotifier preprovisionNotifier,
            IServerIdentityService serverIdentityService,
            IConfiguration configuration)
        {
            _deviceAuthenticatorService = deviceAuthenticatorService;
            _deviceService = cameraService;
            _preprovisionAttemptRepository = preprovisionAttemptRepository;
            _deviceRepository = deviceRepository;
            _logger = logger;
            _unitOfWorkFactory = unitOfWorkFactory;
            _preprovisionNotifier = preprovisionNotifier;
            _serverIdentityService = serverIdentityService;
            _preprovisionAttemptExpirationMinutes = int.Parse(configuration.GetSection("Preprovisioning")["AttemptExpirationMinutes"] ?? throw new ArgumentNullException("Video encoder timeout not configured"));
            _deviceProvisioningUrl = configuration.GetSection("Preprovisioning")["DeviceProvisioningUrl"] ?? throw new ArgumentNullException("Preprovisioning:DeviceProvisioningUrl not configured");
        }

        private string BuildQrPayloadUrl(PreprovisionDetailsDto details)
        {
            var json = JsonSerializer.Serialize(details);
            var jsonBytes = Encoding.UTF8.GetBytes(json);
            var base64Url = Base64Url.EncodeToString(jsonBytes);
            var separator = _deviceProvisioningUrl.Contains('?') ? "&" : "?";
            return $"{_deviceProvisioningUrl}{separator}d={base64Url}";
        }

        public async Task<PreprovisionDetailsDto?> StartPreprovisioningAsync(PreprovisionDeviceModel model)
        {
            if (!IsLocalNetworkInfoValid(model.LocalNetworkInfo))
            {
                _logger.LogWarning("Invalid local network information provided for preprovisioning. LocalNetworkInfo: {@LocalNetworkInfo}", model.LocalNetworkInfo);

                return null;
            }

            var keyPair = _deviceAuthenticatorService.GenerateKeyPair();

            var dto = new CreateDeviceDto()
            {
                DeviceTypeId = model.DeviceTypeId,
                PublicKey = keyPair.PublicKeySpkiBase64,
                PairStatus = DevicePairStatus.PairingPending,
                CreatedAt = DateTime.UtcNow,
                Fingerprint = keyPair.PublicKeyFingerprintHex
            };

            await using var uow = await _unitOfWorkFactory.CreateAsync(useTransaction: true);

            var deviceId = await _deviceService.CreateDeviceAsync(dto, uow);

            var nonce = _deviceAuthenticatorService.GenerateNonceBase64();

            var preprovisionAttemptId = await _preprovisionAttemptRepository.AddPreprovisionAttemptAsync(new CreatePreprovisionAttemptDto()
            {
                DeviceId = deviceId,
                Nonce = nonce,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_preprovisionAttemptExpirationMinutes),
                PreprovisionStatus = PreprovisionStatus.Pending,
                ExpectedNetworkIpv4 = model.LocalNetworkInfo!.LocalIp,
                ExpectedSubnetMask = model.LocalNetworkInfo!.SubnetMask,
            }, uow);

            await uow.CommitAsync();

            var resultDto = new PreprovisionDetailsDto()
            {
                DeviceId = deviceId,
                PreprovisionId = preprovisionAttemptId,
                PrivateKey = keyPair.PrivateKeyBase64,
                ServerIp = model.LocalNetworkInfo.LocalIp.ToString(),
                WifiPassword = model.WifiPassword,
                WifiSSID = model.WifiSSID,
                Nonce = nonce,
                ServerIdentityPublicKey = _serverIdentityService.PublicKeySpkiBase64
            };

            resultDto.QrPayloadUrl = BuildQrPayloadUrl(resultDto);
            resultDto.QRCode = QrCodeHelper.GenerateQrCodeBase64(resultDto.QrPayloadUrl);

            return resultDto;
        }

        public async Task<PreprovisionDetailsDto?> StartPreprovisioningForExistingDeviceAsync(Guid deviceId, PreprovisionDeviceModel model)
        {
            if (!IsLocalNetworkInfoValid(model.LocalNetworkInfo))
            {
                _logger.LogWarning("Invalid local network information provided for preprovisioning. LocalNetworkInfo: {@LocalNetworkInfo}", model.LocalNetworkInfo);

                return null;
            }

            await using var uow = await _unitOfWorkFactory.CreateAsync(useTransaction: true);

            await _preprovisionAttemptRepository.RevokePreprovisionAttemptsAsync(deviceId, uow);

            var keyPair = _deviceAuthenticatorService.GenerateKeyPair();

            var dto = new UpdateDeviceDto()
            {
                Id = deviceId,
                PublicKey = keyPair.PublicKeySpkiBase64,
                PairStatus = DevicePairStatus.PairingPending,
                CreatedAt = DateTime.UtcNow,
                Fingerprint = keyPair.PublicKeyFingerprintHex
            };

            var result = await _deviceService.UpdateDeviceAsync(dto, uow);

            if (!result)
            {
                _logger.LogWarning(
                    "Device {DeviceId} not found during preprovisioning for existing device",
                    deviceId);

                return null;
            }

            string nonce = _deviceAuthenticatorService.GenerateNonceBase64();

            var preprovisionAttemptId = await _preprovisionAttemptRepository.AddPreprovisionAttemptAsync(new CreatePreprovisionAttemptDto()
            {
                DeviceId = deviceId,
                Nonce = nonce,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_preprovisionAttemptExpirationMinutes),
                PreprovisionStatus = PreprovisionStatus.Pending,
                ExpectedNetworkIpv4 = model.LocalNetworkInfo!.LocalIp,
                ExpectedSubnetMask = model.LocalNetworkInfo.SubnetMask,
            }, uow);

            await uow.CommitAsync();

            var resultDto = new PreprovisionDetailsDto()
            {
                DeviceId = deviceId,
                PreprovisionId = preprovisionAttemptId,
                PrivateKey = keyPair.PrivateKeyBase64,
                ServerIp = model.LocalNetworkInfo.LocalIp.ToString(),
                WifiPassword = model.WifiPassword,
                WifiSSID = model.WifiSSID,
                Nonce = nonce,
                ServerIdentityPublicKey = _serverIdentityService.PublicKeySpkiBase64
            };

            resultDto.QrPayloadUrl = BuildQrPayloadUrl(resultDto);
            resultDto.QRCode = QrCodeHelper.GenerateQrCodeBase64(resultDto.QrPayloadUrl);

            return resultDto;
        }

        public async Task<VerifyDeviceResultDto> VerifyDeviceAsync(IPAddress? remoteIp, PreprovisionVerificationModel model)
        {
            var device = await _deviceRepository.GetDeviceByIdWithStatusAsync(model.DeviceId, DevicePairStatus.PairingPending);

            if (device == null)
            {
                _logger.LogWarning(
                    "Device {DeviceId} not found or not pending during verification",
                    model.DeviceId);

                return new VerifyDeviceResultDto()
                {
                    IsValid = false,
                    PreprovisionAttemptId = Guid.Empty
                };
            }

            var validPreprovisionAttempt = device.PreprovisionAttempts
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault(x => x.PreprovisionStatus == PreprovisionStatus.Pending && x.ExpiresAt > DateTime.UtcNow && x.RemainingAttempts > 0);

            if (validPreprovisionAttempt == null)
            {
                _logger.LogWarning(
                    "No valid preprovision attempt found for device {DeviceId} during verification",
                    model.DeviceId);

                return new VerifyDeviceResultDto()
                {
                    IsValid = false,
                    PreprovisionAttemptId = Guid.Empty
                };
            }

            if (!NetworkUtilities.IsClientInSameSubnet(remoteIp, validPreprovisionAttempt.ExpectedNetworkIpv4, validPreprovisionAttempt.ExpectedSubnetMask))
            {
                _logger.LogWarning(
                    "Device {DeviceId} verification from {RemoteIp} rejected; not in subnet {Network}/{Mask}",
                    model.DeviceId,
                    remoteIp,
                    validPreprovisionAttempt.ExpectedNetworkIpv4,
                    validPreprovisionAttempt.ExpectedSubnetMask);

                await _preprovisionAttemptRepository.DecreaseRemainingAttemptsAndRevokeIfDepletedAsync(validPreprovisionAttempt.Id);

                return new VerifyDeviceResultDto()
                {
                    IsValid = false,
                    PreprovisionAttemptId = validPreprovisionAttempt.Id
                };
            }

            var hash = _deviceAuthenticatorService.ComputeBindingHash(model.DeviceId, device.Fingerprint, validPreprovisionAttempt.Nonce);

            var isValid = _deviceAuthenticatorService.VerifySignature(device.PublicKey, hash, model.DeviceSignature);

            if (isValid)
            {
                _preprovisionNotifier.NotifySuccessfulVerification(model.DeviceId);
            }
            else
            {
                await _preprovisionAttemptRepository.DecreaseRemainingAttemptsAndRevokeIfDepletedAsync(validPreprovisionAttempt.Id);
            }

            return new VerifyDeviceResultDto()
            {
                IsValid = isValid,
                PreprovisionAttemptId = validPreprovisionAttempt.Id
            };
        }

        public async Task<bool> FinishPreprovisioningAsync(FinishPreprovisionAttemptDto dto)
        {
            await using var uow = await _unitOfWorkFactory.CreateAsync(useTransaction: true);

            var attemptClaimed = await _preprovisionAttemptRepository.FinishPreprovisionAttemptAsync(dto, uow);

            if (!attemptClaimed)
            {
                await uow.RollbackAsync();
                return false;
            }

            var statusSet = await _deviceRepository.SetDeviceStatusAsync(dto.DeviceId, DevicePairStatus.Paired, uow);

            if (!statusSet)
            {
                await uow.RollbackAsync();
                return false;
            }

            if (!string.IsNullOrWhiteSpace(dto.ClaimedFromIpv4))
            {
                await _deviceRepository.UpdateDeviceIpAsync(dto.DeviceId, dto.ClaimedFromIpv4, uow);
            }

            await uow.CommitAsync();
            return true;
        }

        public async Task<bool> CancelPairingAndDeleteDeviceAsync(Guid deviceId)
        {
            var device = await _deviceRepository.GetDeviceByIdAsync(deviceId);

            if (device == null)
            {
                return false;
            }

            if (device.PairStatus != DevicePairStatus.PairingPending && device.PairStatus != DevicePairStatus.Paired)
            {
                return false;
            }

            if (device.PairStatus == DevicePairStatus.Paired
                && device.UpdatedAt.HasValue
                && device.UpdatedAt.Value.AddMinutes(_gracePeriodForAllowedDeviceDeletionMinutes) < DateTime.UtcNow)
            {
                return false;
            }

            return await _deviceService.DeleteDeviceAsync(deviceId);
        }

        public async Task<ResumeDeviceSetupDto?> GetResumeSetupStateAsync(Guid deviceId)
        {
            var device = await _deviceRepository.GetDeviceByIdAsync(deviceId);

            if (device == null)
            {
                return null;
            }

            if (device.PairStatus != DevicePairStatus.PairingPending)
            {
                return null;
            }

            var attempt = await _preprovisionAttemptRepository.GetLatestPreprovisionAttemptAsync(deviceId);
            var localNetworkInfo = ResolveLocalNetworkInfo(attempt);

            return new ResumeDeviceSetupDto
            {
                DeviceId = deviceId,
                DeviceTypeId = device.DeviceTypeId,
                PairStatus = device.PairStatus,
                LocalNetworkInfo = localNetworkInfo,
            };
        }

        private LocalNetworkInfoDto? ResolveLocalNetworkInfo(PreprovisionAttemptDto? attempt)
        {
            if (attempt?.ExpectedNetworkIpv4 == null || attempt.ExpectedSubnetMask == null)
            {
                return null;
            }

            return NetworkUtilities.GetLocalNetworkInfo()
                .FirstOrDefault(x => x.LocalIp == attempt.ExpectedNetworkIpv4 && x.SubnetMask == attempt.ExpectedSubnetMask);
        }

        private bool IsLocalNetworkInfoValid(LocalNetworkInfoDto? dto)
        {
            if (dto == null)
            {
                return false;
            }

            return NetworkUtilities.GetLocalNetworkInfo()
                .Any(x => x.LocalIp == dto.LocalIp && x.SubnetMask == dto.SubnetMask && x.Gateway == dto.Gateway);
        }
    }
}
