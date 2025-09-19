namespace Freelancing.Services
{
    public interface IPdfService
    {
        Task<byte[]> GenerateHtmlToPdfAsync(string htmlContent, string title = "Report");
    }
}