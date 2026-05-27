namespace CamPortal.Contracts.Dtos.UserDtos
{
    public enum UpdateUserRolesResult
    {
        Success = 0,
        UserNotFound = 1,
        CannotEditSelf = 2,
    }
}
