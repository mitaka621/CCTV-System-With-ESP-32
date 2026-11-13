using System.Security.Claims;

namespace Homewatch.Contracts.Dtos.UserDtos
{
    public class LoginResultDto
    {
        public bool Succeeded { get; set; }

        public bool IsFirstTimeSetup { get; set; }

        public Guid UserId { get; set; }

        public ClaimsPrincipal? Principal { get; set; }
    }
}
