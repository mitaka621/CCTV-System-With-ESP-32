using CamPortal.Contracts.Abstractions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CamPortal.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DeviceTypeIconController : ControllerBase
    {
        private const string _svgContentType = "image/svg+xml";

        private readonly IDeviceTypeIconStorageService _deviceTypeIconStorageService;

        public DeviceTypeIconController(IDeviceTypeIconStorageService deviceTypeIconStorageService)
        {
            _deviceTypeIconStorageService = deviceTypeIconStorageService;
        }

        [HttpGet("{iconName}")]
        public IActionResult Get(string iconName)
        {
            if (string.IsNullOrWhiteSpace(iconName))
            {
                return BadRequest("Icon name is required.");
            }

            var fullPath = _deviceTypeIconStorageService.ResolveIconPath(iconName);
            if (fullPath is null || !System.IO.File.Exists(fullPath))
            {
                return NotFound();
            }

            Response.Headers["Cache-Control"] = "public, max-age=31536000, immutable";

            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return File(stream, _svgContentType);
        }
    }
}
