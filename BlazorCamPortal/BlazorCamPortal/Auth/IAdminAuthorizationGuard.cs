namespace CamPortal.Auth
{
    public interface IAdminAuthorizationGuard
    {
        Task<bool> IsAdminAsync();

        Task<bool> TryAuthorizeAdminAsync();
    }
}
