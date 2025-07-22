using BlazorCamPortal.Contracts.Abstractions.Services;

namespace BlazorCamPortal.Core.Services
{
    public class ScanCoordinatorService : IScanCoordinatorService
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private Task? _currentScanTask;

        public async Task WaitForScanAsync()
        {
            Task? scanTask;

            lock (_semaphore)
            {
                scanTask = _currentScanTask;
            }

            if (scanTask != null)
            {
                await scanTask;
            }
        }

        public async Task<T> RunExclusiveScanAsync<T>(Func<Task<T>> scanTask)
        {
            await _semaphore.WaitAsync();
            try
            {
                _currentScanTask = scanTask();
                return await (Task<T>)_currentScanTask;
            }
            finally
            {
                _currentScanTask = null;
                _semaphore.Release();
            }
        }
    }
}
