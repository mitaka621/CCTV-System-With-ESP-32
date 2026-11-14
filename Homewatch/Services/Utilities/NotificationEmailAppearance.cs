using Homewatch.Contracts.Enums;
using System.Globalization;
using MudColor = MudBlazor.Color;

namespace Homewatch.Core.Utilities
{
    public static class NotificationEmailAppearance
    {
        public const string SurfaceColorHex = "#1e1e2e";

        public static string GetIconColorHex(MudColor iconColor)
        {
            return iconColor switch
            {
                MudColor.Primary => "#776be7",
                MudColor.Secondary => "#ff6ec7",
                MudColor.Tertiary => "#1ed5a6",
                MudColor.Info => "#3299ff",
                MudColor.Success => "#0bba83",
                MudColor.Warning => "#ffa800",
                MudColor.Error => "#f64e62",
                MudColor.Dark => "#9e9eab",
                _ => "#e0e0e6"
            };
        }

        public static string GetTypeLabel(NotificationType notificationType)
        {
            return notificationType switch
            {
                NotificationType.Security => "Security Alert",
                NotificationType.EnvironmentalAlert => "Environmental Alert",
                _ => "System Notification"
            };
        }

        public static string GetTypeColorHex(NotificationType notificationType)
        {
            return notificationType switch
            {
                NotificationType.Security => "#f64e62",
                NotificationType.EnvironmentalAlert => "#ffa800",
                _ => "#3299ff"
            };
        }

        public static string BlendWithSurface(string colorHex, double colorWeight)
        {
            var color = ParseHex(colorHex);
            var surface = ParseHex(SurfaceColorHex);

            var red = (int)Math.Round(color.Red * colorWeight + surface.Red * (1 - colorWeight));
            var green = (int)Math.Round(color.Green * colorWeight + surface.Green * (1 - colorWeight));
            var blue = (int)Math.Round(color.Blue * colorWeight + surface.Blue * (1 - colorWeight));

            return $"#{red:x2}{green:x2}{blue:x2}";
        }

        private static (int Red, int Green, int Blue) ParseHex(string colorHex)
        {
            var value = colorHex.TrimStart('#');

            return (
                int.Parse(value.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                int.Parse(value.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                int.Parse(value.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }
    }
}
