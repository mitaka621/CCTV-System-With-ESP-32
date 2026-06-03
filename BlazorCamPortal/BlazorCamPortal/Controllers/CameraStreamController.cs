using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace CamPortal.Controllers
{
    [Route("api/cameras")]
    [ApiController]
    [Authorize]
    public class CameraStreamController : ControllerBase
    {
        private const string _multipartBoundary = "frame";

        private readonly ICameraFramesManagerService _cameraFramesManagerService;
        private readonly ILogger<CameraStreamController> _logger;

        public CameraStreamController(
            ICameraFramesManagerService cameraFramesManagerService,
            ILogger<CameraStreamController> logger)
        {
            _cameraFramesManagerService = cameraFramesManagerService;
            _logger = logger;
        }

        [HttpGet("{cameraId:guid}/stream")]
        public async Task StreamAsync(Guid cameraId, CancellationToken ct, bool isOriginalResolution)
        {
            Response.ContentType = $"multipart/x-mixed-replace; boundary={_multipartBoundary}";
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Connection"] = "keep-alive";
            Response.Headers["X-Accel-Buffering"] = "no";

            var viewerId = AuthHelper.GetLoggedUserId(User);
            var reader = _cameraFramesManagerService.SubscribeViewer(cameraId, viewerId, isOriginalResolution);

            try
            {
                await foreach (var frame in reader.ReadAllAsync(ct))
                {
                    var header = Encoding.ASCII.GetBytes(
                        $"--{_multipartBoundary}\r\nContent-Type: image/jpeg\r\nContent-Length: {frame.Length}\r\n\r\n");

                    await Response.Body.WriteAsync(header, ct);
                    await Response.Body.WriteAsync(frame, ct);
                    await Response.Body.WriteAsync("\r\n"u8.ToArray(), ct);
                    await Response.Body.FlushAsync(ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error streaming frames for camera {CameraId}", cameraId);
            }
            finally
            {
                _cameraFramesManagerService.UnsubscribeViewer(cameraId, viewerId, isOriginalResolution);
            }
        }
    }
}
