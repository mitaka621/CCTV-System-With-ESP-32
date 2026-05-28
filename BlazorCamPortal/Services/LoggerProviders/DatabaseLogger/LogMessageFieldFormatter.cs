using CamPortal.Infrastructure.Data.Entities;

namespace CamPortal.Core.LoggerProviders.DatabaseLogger
{
    internal static class LogMessageFieldFormatter
    {
        internal static string Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value ?? string.Empty;
            }

            if (maxLength <= 3)
            {
                return value[..maxLength];
            }

            return value[..(maxLength - 3)] + "...";
        }

        internal static string FormatCategory(string category)
            => Truncate(category, LogMessage.MaxCategoryLength);

        internal static string FormatMessage(string message)
            => Truncate(message, LogMessage.MaxMessageLength);

        internal static string? FormatException(string? exception)
        {
            if (exception is null)
            {
                return null;
            }

            return Truncate(exception, LogMessage.MaxExceptionLength);
        }
    }
}
