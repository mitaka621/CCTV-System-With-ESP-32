namespace BlazorCamPortal.Contracts.Abstractions.Services
{
    public interface IScanCoordinatorService
    {
        Task WaitForScanAsync();

        Task<T> RunExclusiveScanAsync<T>(Func<Task<T>> scanTask);
    }
}
