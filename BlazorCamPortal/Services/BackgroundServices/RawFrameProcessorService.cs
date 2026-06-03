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
        private readonly IActiveCameraConnections _activeCameraConnections;

        private readonly int _numberOfWorkers;

        public RawFrameProcessorService(
            ICameraFramesManagerService cameraFramesManagerService,
            ILogger<RawFrameProcessorService> logger,
            IConfiguration configuration,
            IActiveCameraConnections activeCameraConnections)
        {
            _cameraFramesManagerService = cameraFramesManagerService;
            _logger = logger;
            _numberOfWorkers = Math.Max(1, Environment.ProcessorCount / 2);
            _activeCameraConnections = activeCameraConnections;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            List<Task> workerTasks = new();
            for (int i = 0; i < _numberOfWorkers; i++)
            {
                workerTasks.Add(StartWorkerLoopAsync(stoppingToken));
            }

            await Task.WhenAll(workerTasks);
        }

        private async Task StartWorkerLoopAsync(CancellationToken stoppingToken)
        {
            var rawFramesReader = _cameraFramesManagerService.RawFramesChannelReader;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var (device, frame) = await rawFramesReader.ReadAsync(stoppingToken);

                    if (!_activeCameraConnections.IsCameraActive(device.Id))
                        continue;

                    var stampedFrame = _cameraFramesManagerService.StampFrame(frame, device);

                    _cameraFramesManagerService.PublishProcessedFrame(device.Id, stampedFrame);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { _logger.LogError(ex, "Error processing raw camera frames"); }
            }
        }
    }
}
