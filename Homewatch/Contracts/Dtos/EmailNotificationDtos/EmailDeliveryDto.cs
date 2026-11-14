namespace Homewatch.Contracts.Dtos.EmailNotificationDtos
{
    public class EmailDeliveryDto
    {
        public required string SenderAddress { get; set; }

        public required string SenderDisplayName { get; set; }

        public required string AppPassword { get; set; }

        public required List<string> RecipientAddresses { get; set; }

        public string AppBaseUrl { get; set; } = string.Empty;
    }
}
