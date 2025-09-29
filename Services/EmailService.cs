using Microsoft.Extensions.Configuration;
using Resend;

namespace Freelancing.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly IResend _resend;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger, IResend resend)
        {
            _configuration = configuration;
            _logger = logger;
            _resend = resend;
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
                var message = new EmailMessage();
                message.From = $"{fromName} <{fromEmail}>";
                message.To.Add(to);
                message.Subject = subject;

                if (isHtml)
                {
                    message.HtmlBody = body;
                }
                else
                {
                    message.TextBody = body;
                }

                _logger.LogInformation("Sending email to {To} with subject: {Subject}", to, subject);
                await _resend.EmailSendAsync(message);
                _logger.LogInformation("Email sent successfully to {To}", to);
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
                <img src=""https://ik.imagekit.io/6txj3mofs/GIGHub%20(2).png?updatedAt=1749718355580"" alt=""GigHub Logo"" style=""width: 120px; height: auto;"">
                <h2>Welcome to GigHub!</h2>
                <p>Please confirm your email address by clicking the link below:</p>
                <p><a href='{callbackUrl}'>Confirm Email</a></p>
                <p>If you didn't create this account, please ignore this email.</p>
                <p>Best regards,<br/>The GigHub Team</p>";

            await SendEmailAsync(to, subject, body, true);
        }

        public async Task SendPasswordResetAsync(string to, string callbackUrl)
        {
            var subject = "Reset your password";
            var body = $@"
                <img src=""https://ik.imagekit.io/6txj3mofs/GIGHub%20(2).png?updatedAt=1749718355580"" alt=""GigHub Logo"" style=""width: 120px; height: auto;"">
                <h2>Password Reset Request</h2>
                <p>You requested a password reset. Click the link below to reset your password:</p>
                <p><a href='{callbackUrl}'>Reset Password</a></p>
                <p>If you didn't request this, please ignore this email.</p>
                <p>This link will expire in 1 hour.</p>
                <p>Best regards,<br/>The GigHub Team</p>";

            await SendEmailAsync(to, subject, body, true);
        }

        public async Task SendEmailChangeConfirmationAsync(string to, string confirmationLink)
        {
            var subject = "Confirm your new email address";
            var body = $@"
                <img src=""https://ik.imagekit.io/6txj3mofs/GIGHub%20(2).png?updatedAt=1749718355580"" alt=""GigHub Logo"" style=""width: 120px; height: auto;"">
                <h2>Email Change Request</h2>
                <p>You requested to change your email address. Click the link below to confirm your new email:</p>
                <p><a href='{confirmationLink}'>Confirm Email Change</a></p>
                <p>If you didn't request this, please ignore this email.</p>
                <p>This link will expire in 1 hour.</p>
                <p>Best regards,<br/>The GigHub Team</p>";

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