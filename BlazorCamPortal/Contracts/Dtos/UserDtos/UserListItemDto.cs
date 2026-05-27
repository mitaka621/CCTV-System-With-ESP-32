namespace CamPortal.Contracts.Dtos.UserDtos
{
    public class UserListItemDto
    {
        public Guid Id { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public bool IsFirstTimeSetup { get; set; }

        public List<RoleDto> Roles { get; set; } = new();
    }
}
