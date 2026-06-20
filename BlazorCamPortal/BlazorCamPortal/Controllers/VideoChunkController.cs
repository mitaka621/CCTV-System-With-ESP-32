using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CamPortal.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = $"{Roles.User},{Roles.Admin}")]
    public class VideoChunkController : ControllerBase
    {
        private readonly IStorageLocationService _storageLocationService;

        public VideoChunkController(IStorageLocationService storageLocationService)
        {
            _storageLocationService = storageLocationService;
        }

        [HttpGet("chunk/{cameraId}/{chunkName}")]
        [HttpHead("chunk/{cameraId}/{chunkName}")]
        public IActionResult GetChunk(string cameraId, string chunkName)
        {
            if (!_storageLocationService.TryGetChunkFullPath(cameraId, chunkName, out var fullPath))
            {
                return BadRequest("Invalid chunk identifier.");
            }

            return ServeFile(fullPath, "Video chunk not found.");
        }

        [HttpGet("export/{fileName}")]
        [HttpHead("export/{fileName}")]
        public IActionResult GetExport(string fileName)
        {
            if (!_storageLocationService.TryGetExportFullPath(fileName, out var fullPath))
            {
                return BadRequest("Invalid export identifier.");
            }

            return ServeFile(fullPath, "Exported video not found.");
        }

        private IActionResult ServeFile(string fullPath, string notFoundMessage)
        {
            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound(notFoundMessage);
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
