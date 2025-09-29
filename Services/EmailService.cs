using Microsoft.Extensions.Configuration;
using Resend;

namespace Freelancing.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly ResendClient _resendClient;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            // Initialize Resend client
            var apiKey = _configuration["Email:ResendApiKey"] ?? Environment.GetEnvironmentVariable("RESEND_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogError("Resend API key is not configured");
                throw new InvalidOperationException("Resend API key is not configured");
            }

            _resendClient = new ResendClient(apiKey);
        }

        public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = false)
        {
            // Validate input parameters
            if (string.IsNullOrWhiteSpace(to))
            {
                _logger.LogError("Email recipient (to) is null or empty");
                throw new ArgumentException("Email recipient cannot be null or empty", nameof(to));
            }

            if (string.IsNullOrWhiteSpace(subject))
            {
                _logger.LogError("Email subject is null or empty");
                throw new ArgumentException("Email subject cannot be null or empty", nameof(subject));
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                _logger.LogError("Email body is null or empty");
                throw new ArgumentException("Email body cannot be null or empty", nameof(body));
            }

            // Validate email address format
            if (!IsValidEmail(to))
            {
                _logger.LogError("Invalid email address format: {Email}", to);
                throw new ArgumentException($"Invalid email address format: {to}", nameof(to));
            }

            // Get configuration values
            var fromEmail = _configuration["Email:FromEmail"] ?? Environment.GetEnvironmentVariable("FROM_EMAIL");
            var fromName = _configuration["Email:FromName"] ?? "GigHub";

            // Validate configuration
            if (string.IsNullOrWhiteSpace(fromEmail))
            {
                _logger.LogError("From email configuration is missing");
                throw new InvalidOperationException("From email configuration is missing");
            }

            if (!IsValidEmail(fromEmail))
            {
                _logger.LogError("Invalid from email address format: {Email}", fromEmail);
                throw new InvalidOperationException($"Invalid from email address format: {fromEmail}");
            }

            try
            {
                var emailMessage = new EmailMessage
                {
                    From = $"{fromName} <{fromEmail}>",
                    To = new List<string> { to },
                    Subject = subject,
                    HtmlBody = isHtml ? body : null,
                    TextBody = isHtml ? null : body
                };

                _logger.LogInformation("Sending email to {To} with subject: {Subject}", to, subject);

                var response = await _resendClient.Emails.SendAsync(emailMessage);

                if (response.IsSuccess)
                {
                    _logger.LogInformation("Email sent successfully to {To} with ID: {EmailId}", to, response.Data?.Id);
                }
                else
                {
                    _logger.LogError("Failed to send email to {To}. Error: {Error}", to, response.Error?.Message);
                    throw new InvalidOperationException($"Failed to send email: {response.Error?.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while sending email to {To}", to);
                throw new InvalidOperationException($"Failed to send email: {ex.Message}", ex);
            }
        }

        public async Task SendEmailConfirmationAsync(string to, string callbackUrl)
        {
            var subject = "Confirm your email address";
            var body = $@"
                <div style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;"">
                    <div style=""text-align: center; margin-bottom: 30px;"">
                        <img src=""https://ik.imagekit.io/6txj3mofs/GIGHub%20(2).png?updatedAt=1749718355580"" alt=""GigHub Logo"" style=""width: 120px; height: auto;"">
                    </div>
                    <h2 style=""color: #333; text-align: center;"">Welcome to GigHub!</h2>
                    <p style=""color: #666; line-height: 1.6;"">Please confirm your email address by clicking the link below:</p>
                    <div style=""text-align: center; margin: 30px 0;"">
                        <a href='{callbackUrl}' style=""background-color: #007bff; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;"">Confirm Email</a>
                    </div>
                    <p style=""color: #666; line-height: 1.6;"">If you didn't create this account, please ignore this email.</p>
                    <p style=""color: #666; line-height: 1.6;"">Best regards,<br/>The GigHub Team</p>
                </div>";

            await SendEmailAsync(to, subject, body, true);
        }

        public async Task SendPasswordResetAsync(string to, string callbackUrl)
        {
            var subject = "Reset your password";
            var body = $@"
                <div style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;"">
                    <div style=""text-align: center; margin-bottom: 30px;"">
                        <img src=""https://ik.imagekit.io/6txj3mofs/GIGHub%20(2).png?updatedAt=1749718355580"" alt=""GigHub Logo"" style=""width: 120px; height: auto;"">
                    </div>
                    <h2 style=""color: #333; text-align: center;"">Password Reset Request</h2>
                    <p style=""color: #666; line-height: 1.6;"">You requested a password reset. Click the link below to reset your password:</p>
                    <div style=""text-align: center; margin: 30px 0;"">
                        <a href='{callbackUrl}' style=""background-color: #dc3545; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;"">Reset Password</a>
                    </div>
                    <p style=""color: #666; line-height: 1.6;"">If you didn't request this, please ignore this email.</p>
                    <p style=""color: #ff6b6b; line-height: 1.6;"">This link will expire in 1 hour.</p>
                    <p style=""color: #666; line-height: 1.6;"">Best regards,<br/>The GigHub Team</p>
                </div>";

            await SendEmailAsync(to, subject, body, true);
        }

        public async Task SendEmailChangeConfirmationAsync(string to, string confirmationLink)
        {
            var subject = "Confirm your new email address";
            var body = $@"
                <div style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;"">
                    <div style=""text-align: center; margin-bottom: 30px;"">
                        <img src=""https://ik.imagekit.io/6txj3mofs/GIGHub%20(2).png?updatedAt=1749718355580"" alt=""GigHub Logo"" style=""width: 120px; height: auto;"">
                    </div>
                    <h2 style=""color: #333; text-align: center;"">Email Change Request</h2>
                    <p style=""color: #666; line-height: 1.6;"">You requested to change your email address. Click the link below to confirm your new email:</p>
                    <div style=""text-align: center; margin: 30px 0;"">
                        <a href='{confirmationLink}' style=""background-color: #28a745; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;"">Confirm Email Change</a>
                    </div>
                    <p style=""color: #666; line-height: 1.6;"">If you didn't request this, please ignore this email.</p>
                    <p style=""color: #ff6b6b; line-height: 1.6;"">This link will expire in 1 hour.</p>
                    <p style=""color: #666; line-height: 1.6;"">Best regards,<br/>The GigHub Team</p>
                </div>";

            await SendEmailAsync(to, subject, body, true);
        }

        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}