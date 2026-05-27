namespace CamPortal.Contracts.Constants
{
    public static class Roles
    {
        public const string Admin = "Admin";

        public const string User = "User";

        public const string InfoDashboard = "InfoDashboard";

        public static readonly IReadOnlyList<string> All = [Admin, User, InfoDashboard];
    }
}
