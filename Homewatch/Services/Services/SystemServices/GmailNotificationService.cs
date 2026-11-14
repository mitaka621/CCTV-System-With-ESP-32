using Homewatch.Contracts.Constants;
using Homewatch.Contracts.Abstractions.Repositories;
using Homewatch.Contracts.Abstractions.Services;
using Homewatch.Contracts.Dtos.EmailNotificationDtos;
using Homewatch.Contracts.Dtos.NotificationDtos;
using Homewatch.Contracts.Enums;
using Homewatch.Contracts.Models;
using Homewatch.Core.Utilities;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using MimeKit.Utils;
using MudBlazor;
using System.ComponentModel.DataAnnotations;
using System.Net.Sockets;
using System.Security.Cryptography;
using MailKitAuthenticationException = MailKit.Security.AuthenticationException;

namespace Homewatch.Core.Services.SystemServices
{
    public class GmailNotificationService : IEmailNotificationService
    {
        private const int _iconSizeInPixels = 96;
        private const string _defaultSenderDisplayName = "Homewatch";

        private readonly IEmailNotificationSettingsRepository _emailNotificationSettingsRepository;
        private readonly IUserRepository _userRepository;
        private readonly IDataProtector _appPasswordProtector;
        private readonly ILogger<GmailNotificationService> _logger;
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly TimeSpan _timeout;

        public GmailNotificationService(
            IEmailNotificationSettingsRepository emailNotificationSettingsRepository,
            IUserRepository userRepository,
            IDataProtectionProvider dataProtectionProvider,
            IConfiguration configuration,
            ILogger<GmailNotificationService> logger)
        {
            _emailNotificationSettingsRepository = emailNotificationSettingsRepository;
            _userRepository = userRepository;
            _appPasswordProtector = dataProtectionProvider.CreateProtector("Homewatch.EmailNotificationSettings.AppPassword");
            _logger = logger;

            var configurationSection = configuration.GetSection("EmailNotificationConfig");

            _smtpHost = configurationSection["SmtpHost"]
                ?? throw new ArgumentNullException("SmtpHost not configured");

            _smtpPort = int.Parse(configurationSection["SmtpPort"]
                ?? throw new ArgumentNullException("SmtpPort not configured"));

            _timeout = TimeSpan.FromSeconds(int.Parse(configurationSection["TimeoutInSeconds"]
                ?? throw new ArgumentNullException("TimeoutInSeconds not configured")));
        }

        public async Task<EmailNotificationSettingsModel> GetSettingsAsync()
        {
            var settings = await _emailNotificationSettingsRepository.GetSettingsAsync();

            if (settings == null)
            {
                return new EmailNotificationSettingsModel();
            }

            return new EmailNotificationSettingsModel
            {
                SenderAddress = settings.SenderAddress,
                SenderDisplayName = settings.SenderDisplayName,
                HasStoredAppPassword = !string.IsNullOrEmpty(settings.ProtectedAppPassword),
                AppBaseUrl = settings.AppBaseUrl,
                IsEnabled = settings.IsEnabled
            };
        }

        public async Task<EmailOperationResultDto> SaveSettingsAsync(EmailNotificationSettingsModel settings)
        {
            if (!TryValidate(settings, out var validationMessage))
            {
                return CreateResult(false, validationMessage);
            }

            try
            {
                var storedSettings = await _emailNotificationSettingsRepository.GetSettingsAsync();
                var appPassword = NormalizeAppPassword(settings.AppPassword);

                if (string.IsNullOrEmpty(appPassword) && string.IsNullOrEmpty(storedSettings?.ProtectedAppPassword))
                {
                    return CreateResult(false, "App password is required.");
                }

                await _emailNotificationSettingsRepository.SaveSettingsAsync(new EmailNotificationSettingsDto
                {
                    SenderAddress = settings.SenderAddress.Trim(),
                    SenderDisplayName = ResolveDisplayName(settings.SenderDisplayName),
                    ProtectedAppPassword = string.IsNullOrEmpty(appPassword)
                        ? string.Empty
                        : _appPasswordProtector.Protect(appPassword),
                    AppBaseUrl = settings.AppBaseUrl.Trim().TrimEnd('/'),
                    IsEnabled = settings.IsEnabled
                });

                return CreateResult(true, "Email notification settings saved.");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to save email notification settings.");

                return CreateResult(false, "Failed to save email notification settings.");
            }
        }

        public async Task<EmailOperationResultDto> VerifyConnectionAsync(EmailNotificationSettingsModel settings)
        {
            if (!TryValidate(settings, out var validationMessage))
            {
                return CreateResult(false, validationMessage);
            }

            try
            {
                var delivery = await ResolveDeliveryAsync(settings, []);

                using var timeoutSource = new CancellationTokenSource(_timeout);
                await using var client = await ConnectAsync(delivery, timeoutSource.Token);

                await client.DisconnectAsync(true, timeoutSource.Token);

                return CreateResult(true, $"Signed in to Gmail as {delivery.SenderAddress}.");
            }
            catch (Exception exception)
            {
                return CreateFailureResult(exception, "Gmail connection check failed.");
            }
        }

        public async Task<EmailOperationResultDto> SendTestEmailAsync(EmailNotificationSettingsModel settings, Guid userId)
        {
            if (!TryValidate(settings, out var validationMessage))
            {
                return CreateResult(false, validationMessage);
            }

            try
            {
                var recipientAddresses = await GetRecipientAddressesAsync([userId]);

                if (recipientAddresses.Count == 0)
                {
                    return CreateResult(false, "Your account has no valid email address.");
                }

                var delivery = await ResolveDeliveryAsync(settings, recipientAddresses);

                var testNotification = new NotificationDto
                {
                    Id = Guid.NewGuid(),
                    Name = "Test notification",
                    Description = "Your Gmail account is connected to Homewatch. System, security and environmental notifications will be delivered to this inbox while email notifications are enabled.",
                    Icon = Icons.Material.Filled.MarkEmailRead,
                    IconColor = Color.Success,
                    NotificationType = NotificationType.System,
                    RelativeRedirectUrl = AppRoutes.NotificationSettings,
                    DateCreatedUTC = DateTime.UtcNow
                };

                using var timeoutSource = new CancellationTokenSource(_timeout);

                await SendAsync(delivery, testNotification, timeoutSource.Token);

                return CreateResult(true, $"Test email sent to {string.Join(", ", delivery.RecipientAddresses)}.");
            }
            catch (Exception exception)
            {
                return CreateFailureResult(exception, "Sending the test email failed.");
            }
        }

        public async Task<EmailOperationResultDto> SetEnabledAsync(bool isEnabled)
        {
            try
            {
                if (!await _emailNotificationSettingsRepository.SetEnabledAsync(isEnabled))
                {
                    return CreateResult(false, "Save the Gmail settings first.");
                }

                return CreateResult(true, isEnabled ? "Email notifications enabled." : "Email notifications disabled.");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to change the email notifications state.");

                return CreateResult(false, "Failed to change the email notifications state.");
            }
        }

        public async Task<bool> SendNotificationAsync(NotificationDto notification, List<Guid> userIds, CancellationToken cancellationToken = default)
        {
            if (userIds.Count == 0)
            {
                return false;
            }

            try
            {
                var settings = await _emailNotificationSettingsRepository.GetSettingsAsync();

                if (settings == null || !settings.IsEnabled || string.IsNullOrEmpty(settings.ProtectedAppPassword))
                {
                    return false;
                }

                var recipientAddresses = await GetRecipientAddressesAsync(userIds);

                if (recipientAddresses.Count == 0)
                {
                    return false;
                }

                var delivery = new EmailDeliveryDto
                {
                    SenderAddress = settings.SenderAddress,
                    SenderDisplayName = ResolveDisplayName(settings.SenderDisplayName),
                    AppPassword = _appPasswordProtector.Unprotect(settings.ProtectedAppPassword),
                    RecipientAddresses = recipientAddresses,
                    AppBaseUrl = settings.AppBaseUrl
                };

                using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutSource.CancelAfter(_timeout);

                await SendAsync(delivery, notification, timeoutSource.Token);

                return true;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to send email notification {NotificationName}.", notification.Name);

                return false;
            }
        }

        private async Task SendAsync(EmailDeliveryDto delivery, NotificationDto notification, CancellationToken cancellationToken)
        {
            using var message = BuildMessage(delivery, notification);
            await using var client = await ConnectAsync(delivery, cancellationToken);

            await client.SendAsync(new SendRequest(message), cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }

        private async Task<List<string>> GetRecipientAddressesAsync(List<Guid> userIds)
        {
            var emailAddresses = await _userRepository.GetEmailsByUserIdsAsync(userIds);

            return emailAddresses
                .Where(x => MailboxAddress.TryParse(x, out _))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task<SmtpClient> ConnectAsync(EmailDeliveryDto delivery, CancellationToken cancellationToken)
        {
            var client = new SmtpClient
            {
                Timeout = (int)_timeout.TotalMilliseconds
            };

            try
            {
                var secureSocketOptions = _smtpPort == 465
                    ? SecureSocketOptions.SslOnConnect
                    : SecureSocketOptions.StartTls;

                await client.ConnectAsync(_smtpHost, _smtpPort, secureSocketOptions, cancellationToken);
                await client.AuthenticateAsync(delivery.SenderAddress, delivery.AppPassword, cancellationToken);

                return client;
            }
            catch
            {
                await client.DisposeAsync();
                throw;
            }
        }

        private MimeMessage BuildMessage(EmailDeliveryDto delivery, NotificationDto notification)
        {
            var message = new MimeMessage();
            var senderAddress = new MailboxAddress(delivery.SenderDisplayName, delivery.SenderAddress);

            message.From.Add(senderAddress);
            message.To.Add(senderAddress);
            message.Bcc.AddRange(delivery.RecipientAddresses.Select(MailboxAddress.Parse));
            message.Subject = $"[{NotificationEmailAppearance.GetTypeLabel(notification.NotificationType)}] {notification.Name}";

            var bodyBuilder = new BodyBuilder();

            var iconPart = new MimePart("image", "png")
            {
                Content = new MimeContent(new MemoryStream(RasterizeIcon(notification))),
                ContentDisposition = new ContentDisposition(ContentDisposition.Inline),
                ContentTransferEncoding = ContentEncoding.Base64,
                ContentId = MimeUtils.GenerateMessageId(),
                FileName = "notification-icon.png"
            };

            bodyBuilder.LinkedResources.Add(iconPart);

            var absoluteRedirectUrl = NotificationEmailTemplateRenderer.CreateAbsoluteRedirectUrl(notification.RelativeRedirectUrl, delivery.AppBaseUrl);

            bodyBuilder.HtmlBody = NotificationEmailTemplateRenderer.RenderHtml(notification, $"cid:{iconPart.ContentId}", absoluteRedirectUrl);
            bodyBuilder.TextBody = NotificationEmailTemplateRenderer.RenderText(notification, absoluteRedirectUrl);

            message.Body = bodyBuilder.ToMessageBody();

            return message;
        }

        private byte[] RasterizeIcon(NotificationDto notification)
        {
            var colorHex = NotificationEmailAppearance.GetIconColorHex(notification.IconColor);

            try
            {
                return SvgIconRasterizer.RasterizeToPng(notification.Icon, colorHex, _iconSizeInPixels);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Icon of notification {NotificationName} could not be rasterized. The default icon is used.", notification.Name);

                return SvgIconRasterizer.RasterizeToPng(Icons.Material.Filled.Notifications, colorHex, _iconSizeInPixels);
            }
        }

        private async Task<EmailDeliveryDto> ResolveDeliveryAsync(EmailNotificationSettingsModel settings, List<string> recipientAddresses)
        {
            var appPassword = NormalizeAppPassword(settings.AppPassword);

            if (string.IsNullOrEmpty(appPassword))
            {
                var storedSettings = await _emailNotificationSettingsRepository.GetSettingsAsync();

                if (string.IsNullOrEmpty(storedSettings?.ProtectedAppPassword))
                {
                    throw new ValidationException("App password is required.");
                }

                appPassword = _appPasswordProtector.Unprotect(storedSettings.ProtectedAppPassword);
            }

            return new EmailDeliveryDto
            {
                SenderAddress = settings.SenderAddress.Trim(),
                SenderDisplayName = ResolveDisplayName(settings.SenderDisplayName),
                AppPassword = appPassword,
                RecipientAddresses = recipientAddresses,
                AppBaseUrl = settings.AppBaseUrl.Trim()
            };
        }

        private bool TryValidate(EmailNotificationSettingsModel settings, out string message)
        {
            message = string.Empty;

            if (!MiscUtilities.ValidateModel(settings, out ICollection<ValidationResult> results))
            {
                message = string.Join(" ", results.Select(x => x.ErrorMessage));

                return false;
            }

            if (!string.IsNullOrWhiteSpace(settings.AppBaseUrl) && !NotificationEmailTemplateRenderer.IsHttpUrl(settings.AppBaseUrl.Trim()))
            {
                message = "Portal address must be a full http or https address, for example https://myapp.com";

                return false;
            }

            return true;
        }

        private EmailOperationResultDto CreateFailureResult(Exception exception, string logMessage)
        {
            _logger.LogError(exception, logMessage);

            var message = exception switch
            {
                ValidationException => exception.Message,
                MailKitAuthenticationException => "Gmail rejected the sign-in. Check the Gmail address and app password, and make sure 2-Step Verification is turned on for the account.",
                SmtpCommandException smtpCommandException => $"Gmail refused the request: {smtpCommandException.Message}",
                CryptographicException => "The stored app password can no longer be decrypted. Enter the app password again.",
                SocketException or IOException or TimeoutException or OperationCanceledException => $"Could not reach {_smtpHost}:{_smtpPort} within {_timeout.TotalSeconds} seconds. Check the server's internet connection.",
                _ => exception.Message
            };

            return CreateResult(false, message);
        }

        private string NormalizeAppPassword(string appPassword)
        {
            return string.Concat(appPassword.Where(x => !char.IsWhiteSpace(x)));
        }

        private string ResolveDisplayName(string displayName)
        {
            return string.IsNullOrWhiteSpace(displayName) ? _defaultSenderDisplayName : displayName.Trim();
        }

        private EmailOperationResultDto CreateResult(bool succeeded, string message)
        {
            return new EmailOperationResultDto
            {
                Succeeded = succeeded,
                Message = message
            };
        }
    }
}
