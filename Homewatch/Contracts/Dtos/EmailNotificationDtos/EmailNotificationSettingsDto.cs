namespace Homewatch.Contracts.Dtos.EmailNotificationDtos
{
    public class EmailNotificationSettingsDto
    {
        public string SenderAddress { get; set; } = string.Empty;

        public string SenderDisplayName { get; set; } = string.Empty;

        public string ProtectedAppPassword { get; set; } = string.Empty;

        public string AppBaseUrl { get; set; } = string.Empty;

        public bool IsEnabled { get; set; }
    }
}
