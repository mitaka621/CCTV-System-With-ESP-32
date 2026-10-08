using CamPortal.Contracts.Constants;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;

namespace CamPortal.Auth
{
    public class AdminAuthorizationGuard : IAdminAuthorizationGuard
    {
        private readonly AuthenticationStateProvider _authenticationStateProvider;
        private readonly ISnackbar _snackbar;

        public AdminAuthorizationGuard(AuthenticationStateProvider authenticationStateProvider, ISnackbar snackbar)
        {
            _authenticationStateProvider = authenticationStateProvider;
            _snackbar = snackbar;
        }

        public async Task<bool> IsAdminAsync()
        {
            var authenticationState = await _authenticationStateProvider.GetAuthenticationStateAsync();

            return authenticationState.User.Identity?.IsAuthenticated == true
                && authenticationState.User.IsInRole(Roles.Admin);
        }

        public async Task<bool> TryAuthorizeAdminAsync()
        {
            if (await IsAdminAsync())
            {
                return true;
            }

            _snackbar.Add("You don't have permission to change this. Only administrators can change device parameters and server settings.", Severity.Error);

            return false;
        }
    }
}
