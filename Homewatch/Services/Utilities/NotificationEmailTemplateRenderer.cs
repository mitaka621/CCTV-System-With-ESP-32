using Homewatch.Contracts.Dtos.NotificationDtos;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Homewatch.Core.Utilities
{
    public static class NotificationEmailTemplateRenderer
    {
        private const string _templateResourceName = "NotificationEmailTemplate.html";
        private const int _preheaderMaxLength = 120;
        private const string _dateFormat = "dd MMM yyyy, HH:mm";

        private static readonly Lazy<string> _template = new(LoadTemplate);
        private static readonly Regex _redirectSectionRegex = new(
            "<!--REDIRECT_START-->.*?<!--REDIRECT_END-->",
            RegexOptions.Singleline | RegexOptions.Compiled);

        public static string RenderHtml(NotificationDto notification, string iconSource, string? redirectUrl)
        {
            var typeColor = NotificationEmailAppearance.GetTypeColorHex(notification.NotificationType);
            var iconColor = NotificationEmailAppearance.GetIconColorHex(notification.IconColor);

            var html = string.IsNullOrEmpty(redirectUrl)
                ? _redirectSectionRegex.Replace(_template.Value, string.Empty)
                : _template.Value
                    .Replace("<!--REDIRECT_START-->", string.Empty)
                    .Replace("<!--REDIRECT_END-->", string.Empty);

            return new StringBuilder(html)
                .Replace("{{Title}}", Encode(notification.Name))
                .Replace("{{Preheader}}", Encode(CreatePreheader(notification.Description)))
                .Replace("{{Description}}", Encode(notification.Description).ReplaceLineEndings("<br>"))
                .Replace("{{DateCreated}}", Encode(FormatDate(notification.DateCreatedUTC)))
                .Replace("{{BannerLabel}}", Encode(NotificationEmailAppearance.GetTypeLabel(notification.NotificationType)))
                .Replace("{{BannerColor}}", typeColor)
                .Replace("{{BannerBackground}}", NotificationEmailAppearance.BlendWithSurface(typeColor, 0.18))
                .Replace("{{IconBackground}}", NotificationEmailAppearance.BlendWithSurface(iconColor, 0.18))
                .Replace("{{SurfaceColor}}", NotificationEmailAppearance.SurfaceColorHex)
                .Replace("{{IconSource}}", Encode(iconSource))
                .Replace("{{RedirectUrl}}", Encode(redirectUrl ?? string.Empty))
                .ToString();
        }

        public static string RenderText(NotificationDto notification, string? redirectUrl)
        {
            var builder = new StringBuilder()
                .AppendLine(NotificationEmailAppearance.GetTypeLabel(notification.NotificationType).ToUpperInvariant())
                .AppendLine()
                .AppendLine(notification.Name)
                .AppendLine()
                .AppendLine(notification.Description)
                .AppendLine()
                .AppendLine(FormatDate(notification.DateCreatedUTC));

            if (!string.IsNullOrEmpty(redirectUrl))
            {
                builder
                    .AppendLine()
                    .AppendLine($"Open in Portal: {redirectUrl}");
            }

            return builder.ToString();
        }

        public static string? CreateAbsoluteRedirectUrl(string? relativeRedirectUrl, string appBaseUrl)
        {
            if (!RelativeRedirectUrlValidator.IsValid(relativeRedirectUrl)
                || !Uri.TryCreate(appBaseUrl.Trim(), UriKind.Absolute, out var baseUri)
                || !IsHttpUrl(baseUri.AbsoluteUri))
            {
                return null;
            }

            var absoluteUrl = $"{baseUri.GetLeftPart(UriPartial.Path).TrimEnd('/')}{relativeRedirectUrl}";

            if (!Uri.TryCreate(absoluteUrl, UriKind.Absolute, out var absoluteUri)
                || !string.Equals(absoluteUri.GetLeftPart(UriPartial.Authority), baseUri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return absoluteUri.AbsoluteUri;
        }

        public static bool IsHttpUrl(string? url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private static string CreatePreheader(string description)
        {
            var singleLine = description.ReplaceLineEndings(" ");

            return singleLine.Length <= _preheaderMaxLength
                ? singleLine
                : $"{singleLine[.._preheaderMaxLength].TrimEnd()}...";
        }

        private static string FormatDate(DateTime dateCreatedUtc)
        {
            var utcDate = dateCreatedUtc == default
                ? DateTime.UtcNow
                : DateTime.SpecifyKind(dateCreatedUtc, DateTimeKind.Utc);

            var serverLocalDate = TimeZoneInfo.ConvertTimeFromUtc(utcDate, TimeZoneInfo.Local);
            var serverOffset = TimeZoneInfo.Local.GetUtcOffset(utcDate);
            var offsetSign = serverOffset < TimeSpan.Zero ? "-" : "+";

            return $"{serverLocalDate.ToString(_dateFormat, CultureInfo.InvariantCulture)} (UTC{offsetSign}{serverOffset:hh\\:mm})"
                + $" · {utcDate.ToString(_dateFormat, CultureInfo.InvariantCulture)} UTC";
        }

        private static string Encode(string value)
        {
            return WebUtility.HtmlEncode(value);
        }

        private static string LoadTemplate()
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(_templateResourceName)
                ?? throw new InvalidOperationException($"Embedded resource {_templateResourceName} was not found.");

            using var reader = new StreamReader(stream);

            return reader.ReadToEnd();
        }
    }
}
