namespace CamPortal.Contracts.Dtos.UserDtos
{
    public class UserDto
    {
        public Guid Id { get; set; }

        public required string UserName { get; set; }

        public required string Email { get; set; }

        public required string Password { get; set; }

        public bool IsFirstTimeSetup { get; set; }

        public required Guid SecurityStamp { get; set; }

        public List<RoleDto> Roles { get; set; } = new();
    }
}
