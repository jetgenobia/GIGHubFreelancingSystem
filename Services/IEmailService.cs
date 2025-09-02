namespace Freelancing.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body, bool isHtml = false);
        Task SendEmailConfirmationAsync(string to, string callbackUrl);
        Task SendPasswordResetAsync(string to, string callbackUrl);
        Task SendEmailChangeConfirmationAsync(string email, string confirmationLink);
    }
}
