using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Core.Utilities;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace CamPortal.Auth
{
    public class RevalidatingAuthStateProvider : RevalidatingServerAuthenticationStateProvider
    {
        private readonly IUserRepository _userRepository;

        public RevalidatingAuthStateProvider(
            ILoggerFactory loggerFactory,
            IUserRepository userRepository) : base(loggerFactory)
        {
            _userRepository = userRepository;
        }

        protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

        protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState authenticationState, CancellationToken cancellationToken)
        {
            var idStr = authenticationState.User.FindFirst(CustomClaimTypes.Id)?.Value;

            var stampStr = authenticationState.User.FindFirst(CustomClaimTypes.SecurityStamp)?.Value;

            if (!Guid.TryParse(idStr, out var userId) || !Guid.TryParseExact(stampStr, "N", out var cookieStamp))
                return false;

            var dbStamp = await _userRepository.GetSecurityStampAsync(userId);

            return dbStamp != Guid.Empty && dbStamp == cookieStamp;
        }
    }
}
