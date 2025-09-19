using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace Freelancing.Services
{
    public class PdfService : IPdfService
    {
        private readonly ILogger<PdfService> _logger;

        public PdfService(ILogger<PdfService> logger)
        {
            _logger = logger;
        }

        public async Task<byte[]> GenerateHtmlToPdfAsync(string htmlContent, string title = "Report")
        {
            try
            {
                // Download browser if not already downloaded
                await new BrowserFetcher().DownloadAsync();

                using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Headless = true,
                    Args = new[] {
                        "--no-sandbox",
                        "--disable-setuid-sandbox",
                        "--disable-dev-shm-usage"
                    }
                });

                using var page = await browser.NewPageAsync();

                // Set the HTML content
                await page.SetContentAsync(htmlContent, new NavigationOptions
                {
                    WaitUntil = new[] { WaitUntilNavigation.Networkidle0 }
                });

                // Generate PDF with specific options
                var pdfOptions = new PdfOptions
                {
                    Format = PaperFormat.A4,
                    PrintBackground = true,
                    MarginOptions = new MarginOptions
                    {
                        Top = "1cm",
                        Right = "1cm",
                        Bottom = "1cm",
                        Left = "1cm"
                    },
                    DisplayHeaderFooter = true,
                    HeaderTemplate = "<div></div>", // Empty header
                    FooterTemplate = @"
                        <div style='font-size: 10px; width: 100%; text-align: center; color: #666;'>
                            <span>Page <span class='pageNumber'></span> of <span class='totalPages'></span></span>
                        </div>"
                };

                return await page.PdfDataAsync(pdfOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating PDF for {Title}", title);
                throw;
            }
        }
    }
}