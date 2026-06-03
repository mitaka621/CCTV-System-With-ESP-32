namespace CamPortal.Core.Utilities
{
    public static class AuthHelper
    {
        public static Guid GetLoggedUserId(System.Security.Claims.ClaimsPrincipal user)
        {
            var userIdClaim = user.FindFirst(CustomClaimTypes.Id)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }
    }
}
