using CamPortal.Contracts.Abstractions.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CamPortal.Core.BackgroundServices
{
    public class RawFrameProcessorService : BackgroundService
    {
        private readonly ICameraFramesManagerService _cameraFramesManagerService;
        private readonly ILogger<RawFrameProcessorService> _logger;

        public RawFrameProcessorService(
            ICameraFramesManagerService cameraFramesManagerService,
            ILogger<RawFrameProcessorService> logger,
            IConfiguration configuration)
        {
            _cameraFramesManagerService = cameraFramesManagerService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var rawFramesReader = _cameraFramesManagerService.RawFramesChannelReader;

                    var (cameraId, frame) = await rawFramesReader.ReadAsync(stoppingToken);

                    var stampedFrame = _cameraFramesManagerService.StampFrame(frame);

                    _cameraFramesManagerService.PublishProcessedFrame(cameraId, stampedFrame);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { _logger.LogError(ex, "Error processing raw camera frames"); }
            }
        }
    }
}
