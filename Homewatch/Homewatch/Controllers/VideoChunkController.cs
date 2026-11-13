using Homewatch.Contracts.Abstractions.Services;
using Homewatch.Contracts.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Homewatch.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = $"{Roles.User},{Roles.Admin}")]
    [DisableRateLimiting]
    public class VideoChunkController : ControllerBase
    {
        private readonly IStorageLocationService _storageLocationService;
        private readonly IRemoteStorageService _remoteStorageService;
        private readonly ISmbReadSessionManager _smbReadSessionManager;
        private readonly IDeviceService _deviceService;

        public VideoChunkController(
            IStorageLocationService storageLocationService,
            IRemoteStorageService remoteStorageService,
            ISmbReadSessionManager smbReadSessionManager,
            IDeviceService deviceService)
        {
            _storageLocationService = storageLocationService;
            _remoteStorageService = remoteStorageService;
            _smbReadSessionManager = smbReadSessionManager;
            _deviceService = deviceService;
        }

        [HttpGet("chunk/{cameraId}/{chunkName}")]
        [HttpHead("chunk/{cameraId}/{chunkName}")]
        public async Task<IActionResult> GetChunkAsync(string cameraId, string chunkName, [FromQuery] string? sourceTargetId)
        {
            if (string.IsNullOrEmpty(sourceTargetId))
            {
                if (!_storageLocationService.TryGetCameraChunkFullPath(cameraId, chunkName, out var fullPath))
                {
                    return BadRequest("Invalid chunk identifier.");
                }

                return ServeFile(fullPath);
            }

            var target = await _remoteStorageService.GetEnabledTargetAsync(Guid.Parse(sourceTargetId));

            if (target == null)
            {
                return NotFound("Remote storage not found.");
            }

            if (!_storageLocationService.TryGetRemoteCameraChunkRelativePath(cameraId, chunkName, out var relativePath))
            {
                return BadRequest("Invalid chunk identifier.");
            }

            var content = await _smbReadSessionManager.ReadFileAsync(target, relativePath, HttpContext.RequestAborted);

            if (content == null)
            {
                return NotFound("Video chunk not found.");
            }

            return File(content, "video/mp2t", enableRangeProcessing: true);
        }

        [HttpGet("export/{fileName}")]
        [HttpHead("export/{fileName}")]
        public IActionResult GetExport(string fileName)
        {
            if (!_storageLocationService.TryGetExportFullPath(fileName, out var fullPath))
            {
                return BadRequest("Invalid export identifier.");
            }

            return ServeFile(fullPath);
        }

        private IActionResult ServeFile(string fullPath)
        {
            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound();
            }

            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

            var extension = Path.GetExtension(fullPath).ToLowerInvariant();
            var contentType = extension switch
            {
                ".mp4" => "video/mp4",
                ".ts" => "video/mp2t",
                _ => "application/octet-stream"
            };

            return File(stream, contentType, enableRangeProcessing: true);
        }
    }
}
