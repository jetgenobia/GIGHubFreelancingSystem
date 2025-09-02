using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace Freelancing.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = false)
        {
            var smtpServer = _configuration["Email:SmtpServer"];
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"]);
            var smtpUsername = _configuration["Email:SmtpUsername"];
            var smtpPassword = _configuration["Email:SmtpPassword"];
            var fromEmail = _configuration["Email:FromEmail"];
            var fromName = _configuration["Email:FromName"];
            var enableSsl = bool.Parse(_configuration["Email:EnableSsl"]);

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

            await client.SendMailAsync(message);
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
    }
}
