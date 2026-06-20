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

        public string GetChunkFullPath(string cameraFolder, string chunkName)
        {
            return Path.Combine(_footageRoot, cameraFolder, chunkName);
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

        public bool TryGetChunkFullPath(string cameraFolder, string chunkName, out string fullPath)
        {
            fullPath = string.Empty;

            if (!IsSafeSegment(cameraFolder) || !IsSafeSegment(chunkName))
            {
                return false;
            }

            var candidate = Path.GetFullPath(Path.Combine(_footageRoot, cameraFolder, chunkName));

            if (!IsWithin(_footageRoot, candidate))
            {
                return false;
            }

            fullPath = candidate;
            return true;
        }

        public bool TryGetExportFullPath(string exportFileName, out string fullPath)
        {
            fullPath = string.Empty;

            if (!IsSafeSegment(exportFileName))
            {
                return false;
            }

            var candidate = Path.GetFullPath(Path.Combine(_exportsRoot, exportFileName));

            if (!IsWithin(_exportsRoot, candidate))
            {
                return false;
            }

            fullPath = candidate;
            return true;
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
            if (string.IsNullOrWhiteSpace(segment))
            {
                return false;
            }

            if (segment.Contains("..", StringComparison.Ordinal))
            {
                return false;
            }

            if (segment.IndexOf('/') >= 0 || segment.IndexOf('\\') >= 0)
            {
                return false;
            }

            if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return false;
            }

            return Path.GetFileName(segment) == segment;
        }

        private static bool IsWithin(string root, string candidate)
        {
            var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            var normalizedCandidate = Path.GetFullPath(candidate);

            return string.Equals(normalizedCandidate, normalizedRoot, StringComparison.OrdinalIgnoreCase)
                || normalizedCandidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
    }
}
