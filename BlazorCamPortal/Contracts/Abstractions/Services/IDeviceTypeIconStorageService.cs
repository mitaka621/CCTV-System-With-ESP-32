using Microsoft.AspNetCore.Components.Forms;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface IDeviceTypeIconStorageService
    {
        IReadOnlyCollection<string> AllowedExtension { get; }

        IReadOnlyCollection<string> AllowedContentTypes { get; }

        Task<string> SaveAsync(IBrowserFile file, CancellationToken ct);

        Task DeleteAsync(string iconName);

        string? ResolveIconPath(string iconName);

        string BuildPublicUrl(string iconName, DateTime iconUpdatedAt);
    }
}
