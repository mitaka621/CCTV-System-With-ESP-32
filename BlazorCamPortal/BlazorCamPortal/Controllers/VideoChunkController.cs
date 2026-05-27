using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CamPortal.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class VideoChunkController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public VideoChunkController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [HttpGet("file/{*filePath}")]
        [HttpHead("file/{*filePath}")]
        public async Task<IActionResult> GetActionAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return BadRequest("File path is required.");

            var decoded = WebUtility.UrlDecode(filePath);
            var safeRelativePath = decoded.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);

            var baseRoots = new List<string>
            {
                _env.ContentRootPath,
                AppContext.BaseDirectory,
                Directory.GetCurrentDirectory(),
                Path.GetFullPath(Path.Combine(_env.ContentRootPath, ".."))
            };

            string? fullPath = null;
            foreach (var baseRoot in baseRoots.Distinct())
            {
                var candidate = Path.GetFullPath(Path.Combine(baseRoot, safeRelativePath));
                if (System.IO.File.Exists(candidate))
                {
                    fullPath = candidate;
                    break;
                }
            }

            if (fullPath is null)
            {
                return NotFound("Video chunk not found.");
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
