using CamPortal.Contracts.Abstractions.Services;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CamPortal.Core.Services
{
    public class DeviceTypeIconStorageService : IDeviceTypeIconStorageService
    {
        private const string _allowedExtension = ".svg";
        private const string _allowedContentType = "image/svg+xml";

        private readonly string _absoluteIconsFolder;
        private readonly long _maxIconSizeBytes;
        private readonly string _baseApiUrl;

        public DeviceTypeIconStorageService(
            IConfiguration configuration,
            IHostEnvironment hostEnvironment)
        {
            var section = configuration.GetSection("DeviceTypeIconsConfig");

            var iconsFolder = section["IconsFolder"]
                ?? throw new ArgumentNullException("DeviceTypeIconsConfig:IconsFolder not configured");

            _maxIconSizeBytes = long.Parse(
                section["MaxIconSizeBytes"]
                ?? throw new ArgumentNullException("DeviceTypeIconsConfig:MaxIconSizeBytes not configured"));

            _baseApiUrl = section["BaseApiUrl"]
                ?? throw new ArgumentNullException("DeviceTypeIconsConfig:BaseApiUrl not configured");

            _absoluteIconsFolder = Path.IsPathRooted(iconsFolder)
                ? iconsFolder
                : Path.Combine(hostEnvironment.ContentRootPath, iconsFolder);

            Directory.CreateDirectory(_absoluteIconsFolder);
        }

        public async Task<string> SaveAsync(IBrowserFile file, CancellationToken ct)
        {
            if (file is null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            var extension = Path.GetExtension(file.Name).ToLowerInvariant();
            if (extension != _allowedExtension)
            {
                throw new InvalidOperationException("Only .svg files are accepted.");
            }

            if (!string.Equals(file.ContentType, _allowedContentType, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only image/svg+xml content type is accepted.");
            }

            if (file.Size <= 0 || file.Size > _maxIconSizeBytes)
            {
                throw new InvalidOperationException(
                    $"Icon must be between 1 byte and {_maxIconSizeBytes} bytes.");
            }

            var iconName = $"{Guid.NewGuid():N}{_allowedExtension}";
            var fullPath = Path.Combine(_absoluteIconsFolder, iconName);

            await using var sourceStream = file.OpenReadStream(_maxIconSizeBytes, ct);
            await using var destinationStream = new FileStream(
                fullPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);

            await sourceStream.CopyToAsync(destinationStream, ct);

            return iconName;
        }

        public Task DeleteAsync(string iconName)
        {
            if (string.IsNullOrWhiteSpace(iconName))
            {
                return Task.CompletedTask;
            }

            var fullPath = ResolveIconPath(iconName);
            if (fullPath is not null && File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            return Task.CompletedTask;
        }

        public string? ResolveIconPath(string iconName)
        {
            if (string.IsNullOrWhiteSpace(iconName))
            {
                return null;
            }

            var sanitized = Path.GetFileName(iconName);
            if (sanitized != iconName)
            {
                return null;
            }

            return Path.Combine(_absoluteIconsFolder, sanitized);
        }

        public string BuildPublicUrl(string iconName, DateTime iconUpdatedAt)
        {
            return $"{_baseApiUrl}{iconName}?v={iconUpdatedAt.Ticks}";
        }
    }
}
