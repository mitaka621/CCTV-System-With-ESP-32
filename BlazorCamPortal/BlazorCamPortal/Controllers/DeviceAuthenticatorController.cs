using BlazorCamPortal.Contracts.Abstractions.Services;
using BlazorCamPortal.Contracts.Enums;
using Microsoft.AspNetCore.Mvc;

namespace BlazorCamPortal.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DeviceAuthenticatorController : ControllerBase
    {
        private readonly IDeviceAuthenticatorService _deviceAuthenticatorService;
        private readonly ICameraService _cameraService;
        private readonly IScanCoordinatorService _scanCoordinatorService;

        public DeviceAuthenticatorController(
            IDeviceAuthenticatorService deviceAuthenticatorService,
            ICameraService cameraService,
            IScanCoordinatorService scanCoordinatorService)
        {
            _deviceAuthenticatorService = deviceAuthenticatorService;
            _cameraService = cameraService;
            _scanCoordinatorService = scanCoordinatorService;
        }

        [HttpGet("challenge")]
        public async Task<IActionResult> SolveEspChallengeAsync([FromQuery] string espCameraChallenge, string mac)
        {
            await _scanCoordinatorService.WaitForScanAsync();

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString();

            if (ipAddress == null || !await _cameraService.DoesCameraExistWithStatusAsync(ipAddress, mac, PairStatus.ServerChallengeSolved))
            {
                return BadRequest();
            }

            return Ok(new
            {
                Hmac = _deviceAuthenticatorService.ComputeHmac(espCameraChallenge)
            });
        }

        [HttpGet("serverSession")]
        public async Task<IActionResult> GenerateEspSessionTokenAsync([FromQuery] string mac)
        {
            await _scanCoordinatorService.WaitForScanAsync();

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString();

            if (ipAddress == null || !await _cameraService.DoesCameraExistWithStatusAsync(ipAddress, mac, PairStatus.ServerChallengeSolved))
            {
                return BadRequest();
            }

            var sessionToken = await _cameraService.GenerateSessionTokenForDeviceAsync(ipAddress, mac);

            if (sessionToken == null)
            {
                return BadRequest("Unable to generate session token");
            }

            await _cameraService.UpdateCameraStatusAsync(mac, PairStatus.Paired);

            return Ok(new
            {
                SessionToken = sessionToken
            });
        }
    }
}
