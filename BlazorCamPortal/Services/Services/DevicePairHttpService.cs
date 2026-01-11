using System.Text.Json;
using AutoMapper;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Enums;
using CamPortal.Contracts.Models;
using CamPortal.Core.Utilities;
using Microsoft.Extensions.Configuration;

namespace CamPortal.Core.Services
{
    public class DevicePairHttpService : IDevicePairHttpService
    {
        private readonly HttpClient _httpClient;
        private readonly IDeviceAuthenticatorService _deviceAuthenticatorService;
        private readonly ICameraService _cameraService;
        private readonly IMapper _mapper;
        private readonly IScanCoordinatorService _scanCoordinatorService;

        private readonly string _espChallengeUrl;
        private readonly string _espPort;

        public DevicePairHttpService(
            HttpClient httpClient,
            IDeviceAuthenticatorService deviceAuthenticatorService,
            ICameraService cameraService,
            IMapper mapper,
            IConfiguration configuration,
            IScanCoordinatorService scanCoordinatorService)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromSeconds(10);

            _espChallengeUrl = configuration.GetSection("ESPCamera")["ChallengeUrl"] ?? throw new InvalidOperationException("ESP ChallengeUrl is not configured");

            _espPort = configuration.GetSection("ESPCamera")["Port"] ?? throw new InvalidOperationException("ESP Port is not configured");

            _deviceAuthenticatorService = deviceAuthenticatorService;
            _cameraService = cameraService;
            _mapper = mapper;
            _scanCoordinatorService = scanCoordinatorService;
        }

        public async Task<List<Guid>> SendChallengeToAllDevicesAsync()
        {
            return await _scanCoordinatorService.RunExclusiveScanAsync(async () =>
            {
                var networkInfo = NetworkUtilites.GetLocalNetworkInfo();

                var usableHosts = NetworkUtilites.GetNumberOfUsableHosts(networkInfo.SubnetMask);

                var tasks = new List<Task<CameraResponseModel?>>();

                var ipsToSkip = await _cameraService.GetAllActiveCameraIpsAsync();

                for (int i = 1; i <= usableHosts; i++)
                {
                    var gatewayChunks = networkInfo.Gateway.ToString().Split('.');

                    string ip = $"{gatewayChunks[0]}.{gatewayChunks[1]}.{gatewayChunks[2]}.{i}";

                    if (ipsToSkip.Contains(ip))
                    {
                        continue;
                    }

                    tasks.Add(SendChallengeAsync(ip, _deviceAuthenticatorService.GenerateChallenge()));
                }

                var newDevices = new List<Guid>();

                var results = await Task.WhenAll(tasks);

                foreach (var result in results)
                {
                    if (result != null)
                    {
                        var isDeviceValid = _deviceAuthenticatorService
                            .ValidateDeviceResponse(result.Challenge!, result.Hmac);

                        Guid newDeviceId;

                        if (isDeviceValid)
                        {
                            newDeviceId = await _cameraService.CreateCameraAsync(_mapper.Map<CreateCameraModel>(result), PairStatus.ServerChallengeSolved);
                        }
                        else
                        {
                            newDeviceId = await _cameraService.CreateCameraAsync(_mapper.Map<CreateCameraModel>(result), PairStatus.ServerChallengeFailed);
                        }

                        newDevices.Add(newDeviceId);
                    }
                }

                return newDevices;
            });
        }

        private async Task<CameraResponseModel?> SendChallengeAsync(string ip, string challenge)
        {
            string url = $"http://{ip}:{_espPort}{_espChallengeUrl}{challenge}";

            try
            {
                var jsonString = await _httpClient.GetStringAsync(url);

                var deviceResponse = JsonSerializer.Deserialize<CameraResponseModel>(jsonString) ?? throw new InvalidCastException("Incorrect body format");

                deviceResponse.Ipv4Address = ip;

                deviceResponse.Challenge = challenge;

                return deviceResponse;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
