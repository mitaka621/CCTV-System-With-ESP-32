using Microsoft.AspNetCore.Components.Forms;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface IDeviceTypeIconStorageService
    {
        Task<string> SaveAsync(IBrowserFile file, CancellationToken ct);

        Task DeleteAsync(string iconName);

        string? ResolveIconPath(string iconName);

        string BuildPublicUrl(string iconName, DateTime iconUpdatedAt);
    }
}
