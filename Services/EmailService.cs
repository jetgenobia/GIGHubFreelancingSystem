using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace Freelancing.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
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

            // Get configuration values with fallbacks
            var smtpServer = _configuration["Email:SmtpServer"] ?? Environment.GetEnvironmentVariable("SMTP_SERVER");
            var smtpPortString = _configuration["Email:SmtpPort"] ?? "587";
            var smtpUsername = _configuration["Email:SmtpUsername"] ?? Environment.GetEnvironmentVariable("SMTP_USERNAME");
            var smtpPassword = _configuration["Email:SmtpPassword"] ?? Environment.GetEnvironmentVariable("SMTP_PASSWORD");
            var fromEmail = _configuration["Email:FromEmail"] ?? Environment.GetEnvironmentVariable("FROM_EMAIL");
            var fromName = _configuration["Email:FromName"] ?? "GigHub";
            var enableSslString = _configuration["Email:EnableSsl"] ?? "true";

            // Validate configuration
            if (string.IsNullOrWhiteSpace(smtpServer))
            {
                _logger.LogError("SMTP server configuration is missing");
                throw new InvalidOperationException("SMTP server configuration is missing");
            }

            if (string.IsNullOrWhiteSpace(smtpUsername))
            {
                _logger.LogError("SMTP username configuration is missing");
                throw new InvalidOperationException("SMTP username configuration is missing");
            }

            if (string.IsNullOrWhiteSpace(smtpPassword))
            {
                _logger.LogError("SMTP password configuration is missing");
                throw new InvalidOperationException("SMTP password configuration is missing");
            }

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

            // Parse configuration values
            if (!int.TryParse(smtpPortString, out int smtpPort))
            {
                _logger.LogWarning("Invalid SMTP port configuration, using default 587");
                smtpPort = 587;
            }

            if (!bool.TryParse(enableSslString, out bool enableSsl))
            {
                _logger.LogWarning("Invalid EnableSsl configuration, using default true");
                enableSsl = true;
            }

            try
            {
                using var client = new SmtpClient(smtpServer, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                    EnableSsl = enableSsl
                };

                var message = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = isHtml
                };

                message.To.Add(to);

                _logger.LogInformation("Sending email to {To} with subject: {Subject}", to, subject);
                await client.SendMailAsync(message);
                _logger.LogInformation("Email sent successfully to {To}", to);
            }
            catch (SmtpException ex)
            {
                _logger.LogError(ex, "SMTP error occurred while sending email to {To}", to);
                throw new InvalidOperationException($"Failed to send email: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while sending email to {To}", to);
                throw;
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