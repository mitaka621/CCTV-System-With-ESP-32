using Homewatch.Contracts.Enums;
using MudBlazor;

namespace Homewatch.Contracts.Dtos.NotificationDtos
{
    public class NotificationDto
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public required string Description { get; set; }

        public Color IconColor { get; set; }

        public string Icon { get; set; } = Icons.Material.Filled.Info;

        public string? RelativeRedirectUrl { get; set; }

        public DateTime DateCreatedUTC { get; set; }

        public NotificationType NotificationType { get; set; }

        public byte[]? TopicIdentifier { get; set; }
    }
}
