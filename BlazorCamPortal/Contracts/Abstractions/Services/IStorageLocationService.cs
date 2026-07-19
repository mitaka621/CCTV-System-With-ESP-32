namespace CamPortal.Contracts.Abstractions.Services
{
    public interface IStorageLocationService
    {
        string StorageRoot { get; }

        string PlaceholderFolderName { get; }

        string GetCameraChunkDirectory(Guid cameraId);

        string GetCameraChunkStagingDirectory(Guid cameraId);

        string GetCameraChunkDayDirectory(Guid cameraId, DateTime utcDate);

        string GetPlaceholderChunkFileName(double durationSeconds);

        string GetPlaceholderChunkFullPath(double durationSeconds);

        string GetExportFullPath(string exportFileName);

        string BuildChunkUrl(string cameraFolder, string chunkName);

        string BuildExportUrl(string exportFileName);

        bool TryGetCameraChunkFullPath(string cameraFolder, string fileName, out string fullPath);

        bool TryGetExportFullPath(string exportFileName, out string fullPath);
    }
}
