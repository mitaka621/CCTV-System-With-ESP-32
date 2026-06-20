namespace CamPortal.Contracts.Abstractions.Services
{
    public interface IStorageLocationService
    {
        string StorageRoot { get; }

        string PlaceholderFolderName { get; }

        string GetCameraChunkDirectory(Guid cameraId);

        string GetChunkFullPath(string cameraFolder, string chunkName);

        string GetPlaceholderChunkFileName(double durationSeconds);

        string GetPlaceholderChunkFullPath(double durationSeconds);

        string GetExportFullPath(string exportFileName);

        string BuildChunkUrl(string cameraFolder, string chunkName);

        string BuildExportUrl(string exportFileName);

        bool TryGetChunkFullPath(string cameraFolder, string chunkName, out string fullPath);

        bool TryGetExportFullPath(string exportFileName, out string fullPath);
    }
}
