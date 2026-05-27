namespace CamPortal.Contracts.Dtos.UserDtos
{
    public enum DeleteUserResult
    {
        Success = 0,
        UserNotFound = 1,
        CannotDeleteSelf = 2,
        CannotDeleteLastAdmin = 3,
    }
}
