using CamPortal.Contracts.Abstractions.Services;
using Microsoft.Extensions.Configuration;

namespace CamPortal.Core.Services.Video
{
    public class StorageLocationService : IStorageLocationService
    {
        private readonly string _storageRoot;
        private readonly string _footageRoot;
        private readonly string _exportsRoot;
        private readonly string _placeholderFolderName;
        private readonly string _placeholderFileNamePattern;
        private readonly string _chunkBaseApiUrl;
        private readonly string _exportBaseApiUrl;

        public StorageLocationService(IConfiguration configuration)
        {
            var configuredRoot = configuration.GetSection("ServerStorage")["RootPath"] ?? string.Empty;
            _storageRoot = ResolveRoot(configuredRoot);

            var footageFolder = configuration.GetSection("VideoEncoderConfig")["VideoChunksFolder"]
                ?? throw new ArgumentNullException("VideoChunksFolder not configured");

            var exportsFolder = configuration.GetSection("VideoExportConfig")["ExportsFolder"]
                ?? throw new ArgumentNullException("ExportsFolder not configured");

            _placeholderFolderName = configuration.GetSection("VideoEncoderConfig")["PlaceholderChunksFolder"]
                ?? throw new ArgumentNullException("PlaceholderChunksFolder not configured");

            _placeholderFileNamePattern = configuration.GetSection("VideoEncoderConfig")["PlaceholderChunkFileNamePattern"]
                ?? throw new ArgumentNullException("PlaceholderChunkFileNamePattern not configured");

            _chunkBaseApiUrl = configuration.GetSection("VideoEncoderConfig")["VideoChunksBaseApiUrl"]
                ?? throw new ArgumentNullException("VideoChunksBaseApiUrl not configured");

            _exportBaseApiUrl = configuration.GetSection("VideoExportConfig")["ExportBaseApiUrl"]
                ?? throw new ArgumentNullException("ExportBaseApiUrl not configured");

            _footageRoot = Path.GetFullPath(Path.Combine(_storageRoot, footageFolder));
            _exportsRoot = Path.GetFullPath(Path.Combine(_storageRoot, exportsFolder));
        }

        public string StorageRoot => _storageRoot;

        public string PlaceholderFolderName => _placeholderFolderName;

        public string GetCameraChunkDirectory(Guid cameraId)
        {
            return Path.Combine(_footageRoot, cameraId.ToString());
        }

        public string GetCameraChunkStagingDirectory(Guid cameraId)
        {
            return Path.Combine(GetCameraChunkDirectory(cameraId), "_staging");
        }

        public string GetCameraChunkDayDirectory(Guid cameraId, DateTime utcDate)
        {
            return Path.Combine(GetCameraChunkDirectory(cameraId), utcDate.ToString("yyyy-MM-dd"));
        }

        public string GetPlaceholderChunkFileName(double durationSeconds)
        {
            return string.Format(_placeholderFileNamePattern, durationSeconds);
        }

        public string GetPlaceholderChunkFullPath(double durationSeconds)
        {
            return Path.Combine(_footageRoot, _placeholderFolderName, GetPlaceholderChunkFileName(durationSeconds));
        }

        public string GetExportFullPath(string exportFileName)
        {
            return Path.Combine(_exportsRoot, exportFileName);
        }

        public string BuildChunkUrl(string cameraFolder, string chunkName)
        {
            return $"{_chunkBaseApiUrl}{cameraFolder}/{chunkName}";
        }

        public string BuildExportUrl(string exportFileName)
        {
            return $"{_exportBaseApiUrl}{exportFileName}";
        }

        public bool TryGetCameraChunkFullPath(string cameraFolder, string fileName, out string fullPath)
        {
            fullPath = string.Empty;

            if (!IsSafeSegment(cameraFolder) || !IsSafeSegment(fileName))
            {
                return false;
            }

            fullPath = cameraFolder == _placeholderFolderName
                ? Path.Combine(_footageRoot, _placeholderFolderName, fileName)
                : Path.Combine(_footageRoot, cameraFolder, GetChunkDayFolder(fileName), fileName);

            return true;
        }

        public bool TryGetExportFullPath(string exportFileName, out string fullPath)
        {
            fullPath = string.Empty;

            if (!IsSafeSegment(exportFileName))
            {
                return false;
            }

            fullPath = Path.Combine(_exportsRoot, exportFileName);
            return true;
        }

        private static string GetChunkDayFolder(string fileName)
        {
            const string marker = "_=";
            var start = fileName.IndexOf(marker, StringComparison.Ordinal) + marker.Length;

            return fileName.Substring(start, 10);
        }

        private static string ResolveRoot(string configuredRoot)
        {
            if (string.IsNullOrWhiteSpace(configuredRoot))
            {
                return Directory.GetCurrentDirectory();
            }

            return Path.GetFullPath(configuredRoot, Directory.GetCurrentDirectory());
        }

        private static bool IsSafeSegment(string segment)
        {
            if (string.IsNullOrWhiteSpace(segment) || segment.Contains("..", StringComparison.Ordinal))
            {
                return false;
            }

            if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return false;
            }

            return Path.GetFileName(segment) == segment;
        }
    }
}
