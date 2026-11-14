namespace Homewatch.Contracts.Constants
{
    public static class AppRoutes
    {
        private const string _deviceConfigBase = "/deviceConfig";

        public const string Dashboard = "/";

        public const string Login = "/login";

        public const string AccessDenied = "/access-denied";

        public const string ChangePassword = "/change-password";

        public const string Error = "/Error";

        public const string UserSettings = "/account/settings";

        public const string InfoDashboard = "/info-dashboard";

        public const string Replay = "/replay";

        public const string AllDevicesConfig = "/allDevicesConfig";

        public const string DeviceConfigTemplate = _deviceConfigBase + "/{DeviceId:guid}";

        public const string Settings = "/system/settings";

        public const string DeviceTypesSettings = Settings + "/deviceTypes";

        public const string NotificationSettings = Settings + "/notifications";

        public const string RetentionSettings = Settings + "/retention";

        public const string SecuritySettings = Settings + "/security";

        public const string UsersSettings = Settings + "/users";

        public static string DeviceConfig(Guid deviceId) => $"{_deviceConfigBase}/{deviceId}";

        public static string LoginWithError(string error) => $"{Login}?error={Uri.EscapeDataString(error)}";

        public static string LoginWithReturnUrl(string baseRelativePath) => $"{Login}?returnUrl=/{Uri.EscapeDataString(baseRelativePath)}";

        public static string ChangePasswordWithError(string error) => $"{ChangePassword}?error={Uri.EscapeDataString(error)}";
    }
}
