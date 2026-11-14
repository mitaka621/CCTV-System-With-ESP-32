namespace Homewatch.Core.Utilities
{
    public static class RelativeRedirectUrlValidator
    {
        public const int MaximumLength = 500;

        public static bool IsValid(string? relativeRedirectUrl)
        {
            if (string.IsNullOrEmpty(relativeRedirectUrl) || relativeRedirectUrl.Length > MaximumLength)
            {
                return false;
            }

            if (relativeRedirectUrl[0] != '/' || relativeRedirectUrl.StartsWith("//", StringComparison.Ordinal))
            {
                return false;
            }

            if (relativeRedirectUrl.Any(character => character == '\\' || char.IsWhiteSpace(character) || char.IsControl(character)))
            {
                return false;
            }

            return Uri.TryCreate(relativeRedirectUrl, UriKind.Relative, out _);
        }

        public static string? Normalize(string? relativeRedirectUrl)
        {
            if (string.IsNullOrWhiteSpace(relativeRedirectUrl))
            {
                return null;
            }

            var trimmedUrl = relativeRedirectUrl.Trim();

            if (!IsValid(trimmedUrl))
            {
                throw new ArgumentException(
                    $"Relative redirect url '{trimmedUrl}' must be a path within the app that starts with a single '/', for example '/system/settings'.",
                    nameof(relativeRedirectUrl));
            }

            return trimmedUrl;
        }
    }
}
