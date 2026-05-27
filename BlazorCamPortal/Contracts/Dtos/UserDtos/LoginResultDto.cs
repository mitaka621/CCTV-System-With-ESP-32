using System.Security.Claims;

namespace CamPortal.Contracts.Dtos.UserDtos
{
    public class LoginResultDto
    {
        public bool Succeeded { get; set; }

        public bool IsFirstTimeSetup { get; set; }

        public Guid UserId { get; set; }

        public ClaimsPrincipal? Principal { get; set; }
    }
}
