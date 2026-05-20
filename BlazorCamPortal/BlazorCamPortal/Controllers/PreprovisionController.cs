using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.PreprovisionAttemptDtos;
using CamPortal.Contracts.Models;
using CamPortal.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CamPortal.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [LocalNetworkOnly]
    [EnableRateLimiting("preprovision-per-ip")]
    public class PreprovisionController : ControllerBase
    {
        private readonly IDevicePreProvisionService _deviceProvisionService;

        public PreprovisionController(IDevicePreProvisionService deviceProvisionService)
        {
            _deviceProvisionService = deviceProvisionService;
        }

        [HttpPost]
        public async Task<IActionResult> VerifySignatureFromPreprovisioning([FromBody] PreprovisionVerificationModel model)
        {
            var remoteIp = HttpContext.Connection.RemoteIpAddress;

            if (remoteIp != null && remoteIp.IsIPv4MappedToIPv6)
            {
                remoteIp = remoteIp.MapToIPv4();
            }

            var result = await _deviceProvisionService.VerifyDeviceAsync(remoteIp, model);

            if (!result.IsValid)
            {
                return BadRequest();
            }

            var finishResult = await _deviceProvisionService.FinishPreprovisioningAsync(new FinishPreprovisionAttemptDto()
            {
                DeviceId = model.DeviceId,
                ClaimedAt = DateTime.UtcNow,
                ClaimedFromIpv4 = remoteIp?.ToString(),
                PreprovisionAttemptId = result.PreprovisionAttemptId,
                PreprovisionStatus = Contracts.Enums.PreprovisionStatus.Claimed
            });

            if (!finishResult)
            {
                return Conflict();
            }

            return Ok();
        }
    }
}
