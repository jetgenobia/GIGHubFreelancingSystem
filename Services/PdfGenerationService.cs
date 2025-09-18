using Freelancing.Models.Entities;
using System.Text;
using PuppeteerSharp;

namespace Freelancing.Services
{
    public class PdfGenerationService : IPdfGenerationService, IDisposable
    {
        private static bool _browserInitialized = false;
        private static IBrowser? _browser;
        private bool _disposed = false;

        public async Task<byte[]> GenerateContractPdfAsync(Contract contract)
        {
            var htmlContent = BuildContractHtml(contract);
            return await GenerateFromHtmlAsync(htmlContent);
        }

        public async Task<byte[]> GenerateFromHtmlAsync(string htmlContent)
        {
            try
            {
                // Initialize browser if not already done
                if (!_browserInitialized)
                {
                    await InitializeBrowserAsync();
                }

                if (_browser == null)
                {
                    throw new InvalidOperationException("Browser failed to initialize");
                }

                // Create a new page
                using var page = await _browser.NewPageAsync();

                // Set content and wait for it to load
                await page.SetContentAsync(htmlContent);
                await page.WaitForTimeoutAsync(1000); // Wait for any dynamic content

                // Emulate print media so @page and print CSS are honored
                await page.EmulateMediaTypeAsync(PuppeteerSharp.Media.MediaType.Print);

                // Generate PDF with proper settings
                var pdfBytes = await page.PdfDataAsync(new PuppeteerSharp.PdfOptions
                {
                    PreferCSSPageSize = true,
                    PrintBackground = true
                });

                // Validate the generated PDF
                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    throw new InvalidOperationException("Generated PDF is empty");
                }

                // Basic PDF validation - check for PDF header
                if (pdfBytes.Length >= 4)
                {
                    var header = System.Text.Encoding.ASCII.GetString(pdfBytes, 0, 4);
                    if (header != "%PDF")
                    {
                        throw new InvalidOperationException("Generated content is not a valid PDF");
                    }
                }

                return pdfBytes;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating PDF: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw new InvalidOperationException($"Failed to generate PDF: {ex.Message}", ex);
            }
        }

        public async Task<byte[]> AddSignaturesToPdfAsync(byte[] pdfData, string? clientSignature, string? freelancerSignature)
        {
            // This would modify the existing PDF to add signature images
            // For now, we'll return the original PDF data
            // In a full implementation, you might want to use a PDF manipulation library
            await Task.Delay(50); // Simulate processing
            return pdfData;
        }

        public async Task<bool> ValidatePdfIntegrityAsync(byte[] pdfData)
        {
            // Validate PDF structure and integrity
            if (pdfData == null || pdfData.Length == 0)
                return false;

            // Check if the data starts with PDF magic number
            if (pdfData.Length >= 4)
            {
                var pdfHeader = System.Text.Encoding.ASCII.GetString(pdfData, 0, 4);
                return pdfHeader == "%PDF";
            }

            return false;
        }

        private string BuildContractHtml(Contract contract)
        {
            var html = new StringBuilder();
            
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset='utf-8'>");
            html.AppendLine("<title>Freelance Contract</title>");
            html.AppendLine("<style>");
            html.AppendLine(@"
                @page { margin: 0.5in; }
                * {
                    margin: 0;
                    padding: 0;
                    box-sizing: border-box;
                }
                html, body { 
                    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; 
                    margin: 0; 
                    padding: 20px; 
                    line-height: 1.6; 
                    color: #333; 
                    font-size: 12pt;
                    background: white;
                    overflow-x: hidden;
                }
                .contract-container {
                    max-width: 800px;
                    margin: 0 auto;
                    background: white;
                    padding: 0;
                }
                .contract-header-logo {
                    text-align: center;
                    margin-bottom: 1rem;
                    padding-bottom: 1rem;
                    border-bottom: 2px solid #1d4ed8;
                }
                .logo-image {
                    max-width: 200px;
                    max-height: 80px;
                    object-fit: contain;
                    margin-bottom: 1rem;
                }
                .company-info {
                    font-size: 10pt;
                    color: #6b7280;
                    line-height: 1.4;
                }
                .mb-6 { margin-bottom: 1.5rem; }
                .mb-2 { margin-bottom: 0.5rem; }
                .mb-1 { margin-bottom: 0.25rem; }
                .mt-4 { margin-top: 1rem; }
                .text-center { text-align: center; }
                .font-bold { font-weight: bold; }
                .italic { font-style: italic; }
                .rounded-lg { border-radius: 0.5rem; }
                .bg-blue-700 { background-color: #1d4ed8; }
                .text-white { color: white; }
                .p-2 { padding: 0.5rem; }
                .parties-container { 
                    display: flex; 
                    justify-content: space-between; 
                    gap: 20px; 
                    margin: 15px 0;
                }
                .party-info { 
                    flex: 1; 
                    padding: 15px;
                    border: 1px solid #e5e7eb;
                    border-radius: 0.375rem;
                    background: #f9fafb;
                }
                .project-description {
                    margin: 10px 0;
                    padding: 10px;
                    background: #f3f4f6;
                    border-left: 4px solid #3b82f6;
                    border-radius: 0.25rem;
                }
                .terms-section h3 {
                    font-weight: bold;
                    margin-bottom: 0.5rem;
                    margin-top: 1rem;
                    color: #1f2937;
                    font-size: 13pt;
                }
                .terms-section ul {
                    margin: 10px 0;
                    padding-left: 20px;
                }
                .terms-section li {
                    margin: 5px 0;
                }
                .signature-section { 
                    margin-top: 50px; 
                    page-break-inside: avoid;
                }
                .signature-box { 
                    border: 1px solid #d1d5db; 
                    padding: 20px; 
                    margin: 20px 0; 
                    min-height: 80px; 
                    background: #f9fafb;
                    border-radius: 0.375rem;
                }
                .signature-label { 
                    font-weight: bold; 
                    margin-bottom: 10px; 
                    color: #374151;
                    font-size: 13pt;
                }
                .signature-image { 
                    max-width: 200px; 
                    max-height: 60px; 
                    border: 1px solid #d1d5db;
                    padding: 5px;
                    background: white;
                    border-radius: 0.25rem;
                }
                .signature-details { 
                    font-size: 10pt; 
                    color: #6b7280; 
                    margin-top: 10px; 
                    font-style: italic;
                }
                .document-footer {
                    margin-top: 40px;
                    font-size: 9pt;
                    color: #6b7280;
                    border-top: 1px solid #e5e7eb;
                    padding-top: 10px;
                    text-align: center;
                }
                @media print {
                    body { margin: 0.5in; }
                    .signature-section { page-break-inside: avoid; }
                }
            ");
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");

            html.AppendLine("<div class='contract-header-logo'>");
            html.AppendLine("<img src='https://ik.imagekit.io/6txj3mofs/GIGHub%20(2).png?updatedAt=1749718355580' class='logo-image' alt='GIGHub Logo' />");
            html.AppendLine("<div class='company-info'>");
            html.AppendLine("<strong>GIGHub Freelancing Platform | Website: https://www.gighub.com/</strong><br>");
            html.AppendLine("</div>");
            html.AppendLine("</div>");

            html.AppendLine("<div class='contract-container'>");

            if (!string.IsNullOrEmpty(contract.ContractContent))
            {
                html.AppendLine(contract.ContractContent);
            }
            else
            {
                // Fallback to basic structure if no contract content is available
                html.AppendLine("<div class='contract-header mb-6'>");
                html.AppendLine($"<h1 class='text-center font-bold mb-2'>{contract.ContractTitle}</h1>");
                html.AppendLine($"<p>This {contract.ContractTitle} (\"Agreement\") is entered into on <strong>{DateTime.Now:MMMM dd, yyyy}</strong> by and between:</p>");
                html.AppendLine("</div>");
                
                // Parties Section
                html.AppendLine("<div class='parties-section mb-6'>");
                html.AppendLine("<div class='rounded-lg bg-blue-700 p-2 mb-2'>");
                html.AppendLine("<h2 class='text-center text-white font-bold'>PARTIES</h2>");
                html.AppendLine("</div>");
                
                html.AppendLine("<div class='parties-container'>");
                html.AppendLine("<div class='party-info'>");
                html.AppendLine("<h3 class='italic'>Client:</h3>");
                html.AppendLine($"<p><strong>Name:</strong> {contract.Project?.User?.FirstName} {contract.Project?.User?.LastName}</p>");
                html.AppendLine($"<p><strong>Email:</strong> {contract.Project?.User?.Email}</p>");
                html.AppendLine("</div>");
                
                html.AppendLine("<div class='party-info'>");
                html.AppendLine("<h3 class='italic'>Freelancer:</h3>");
                html.AppendLine($"<p><strong>Name:</strong> {contract.Bidding?.User?.FirstName} {contract.Bidding?.User?.LastName}</p>");
                html.AppendLine($"<p><strong>Email:</strong> {contract.Bidding?.User?.Email}</p>");
                html.AppendLine("</div>");
                html.AppendLine("</div>");
                html.AppendLine("</div>");
                
                // Project Section
                html.AppendLine("<div class='project-section mb-6'>");
                html.AppendLine("<div class='rounded-lg bg-blue-700 p-2 mb-2'>");
                html.AppendLine("<h2 class='text-center text-white font-bold'>PROJECT DETAILS</h2>");
                html.AppendLine("</div>");
                
                html.AppendLine($"<p><strong>Project Name:</strong> {contract.Project?.ProjectName}</p>");
                html.AppendLine("<p><strong>Description:</strong></p>");
                html.AppendLine($"<div class='project-description'>{contract.Project?.ProjectDescription}</div>");
                html.AppendLine("</div>");

                // Terms Section
                html.AppendLine("<div class='terms-section mb-6'>");
                html.AppendLine("<h2 class='text-center text-white font-bold'>TERMS AND CONDITIONS</h2>");
                html.AppendLine("</div>");
                
                // Payment Terms
                html.AppendLine("<h3 class='mb-1 italic'>1. Payment Terms</h3>");
                html.AppendLine($"<p><strong>Total Development Cost:</strong> ₱{contract.Project?.Budget:N0}</p>");
                if (!string.IsNullOrEmpty(contract.PaymentTerms))
                {
                    html.AppendLine($"<div class='project-description'>{contract.PaymentTerms}</div>");
                }
                
                // Timeline
                html.AppendLine("<h3 class='mb-1 mt-4 italic'>2. Development Timeline</h3>");
                if (!string.IsNullOrEmpty(contract.Timeline))
                {
                    html.AppendLine($"<div class='project-description'>{contract.Timeline}</div>");
                }
                html.AppendLine("<p>Timeline includes development, testing, and deployment phases.</p>");
                
                // Revision Policy
                html.AppendLine("<h3 class='mb-1 mt-4 italic'>3. Revision Policy</h3>");
                html.AppendLine("<p>Standard revision policy includes 3 rounds of revisions. Additional revisions may incur additional charges.</p>");
                
                // Technical Deliverables
                html.AppendLine("<h3 class='mb-1 mt-4 italic'>4. Technical Deliverables</h3>");
                html.AppendLine("<ul>");
                html.AppendLine("<li>Fully functional website/application/software/other IT related projects</li>");
                html.AppendLine("<li>Source code and documentation</li>");
                html.AppendLine("<li>Testing and quality assurance</li>");
                html.AppendLine("<li>Deployment and launch support</li>");
                html.AppendLine("</ul>");
                
                html.AppendLine("</div>");
            }
            
            // Signatures section - Always include signatures regardless of content source
            html.AppendLine("<div class='signature-section'>");
            html.AppendLine("<div class='rounded-lg bg-blue-700 p-2 mb-2'>");
            html.AppendLine("<h2 class='text-center text-white font-bold'>SIGNATURES</h2>");
            html.AppendLine("</div>");
            
            // Client signature
            html.AppendLine("<div class='signature-box'>");
            html.AppendLine("<div class='signature-label'>Client Signature:</div>");
            if (contract.ClientSignedAt.HasValue && !string.IsNullOrEmpty(contract.ClientSignatureData))
            {
                if (contract.ClientSignatureType == "Canvas")
                {
                    html.AppendLine($"<img src='data:image/png;base64,{contract.ClientSignatureData}' class='signature-image' alt='Client Signature' />");
                }
                else
                {
                    html.AppendLine($"<div style='font-family: cursive; font-size: 20px;'>{contract.ClientSignatureData}</div>");
                }
                html.AppendLine($"<div class='signature-details'>Signed on: {contract.ClientSignedAt:MMMM dd, yyyy 'at' HH:mm}</div>");
            }
            else
            {
                html.AppendLine("<div style='color: #999; font-style: italic;'>Pending signature</div>");
            }
            html.AppendLine("</div>");
            
            // Freelancer signature
            html.AppendLine("<div class='signature-box'>");
            html.AppendLine("<div class='signature-label'>Freelancer Signature:</div>");
            if (contract.FreelancerSignedAt.HasValue && !string.IsNullOrEmpty(contract.FreelancerSignatureData))
            {
                if (contract.FreelancerSignatureType == "Canvas")
                {
                    html.AppendLine($"<img src='data:image/png;base64,{contract.FreelancerSignatureData}' class='signature-image' alt='Freelancer Signature' />");
                }
                else
                {
                    html.AppendLine($"<div style='font-family: cursive; font-size: 20px;'>{contract.FreelancerSignatureData}</div>");
                }
                html.AppendLine($"<div class='signature-details'>Signed on: {contract.FreelancerSignedAt:MMMM dd, yyyy 'at' HH:mm}</div>");
            }
            else
            {
                html.AppendLine("<div style='color: #999; font-style: italic;'>Pending signature</div>");
            }
            html.AppendLine("</div>");
            
            html.AppendLine("</div>");
            
            // Document integrity
            if (!string.IsNullOrEmpty(contract.DocumentHash))
            {
                html.AppendLine("<div class='document-footer'>");
                html.AppendLine($"Document Hash: {contract.DocumentHash}<br>");
                html.AppendLine($"Contract ID: {contract.Id}<br>");
                html.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                html.AppendLine("</div>");
            }
            
            html.AppendLine("</div>"); // Close contract-container
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }

        private async Task InitializeBrowserAsync()
        {
            try
            {
                Console.WriteLine("Starting browser initialization...");
                
                // Download browser if not exists
                var fetcher = new BrowserFetcher();
                await fetcher.DownloadAsync();
                Console.WriteLine("Browser downloaded successfully");

                // Launch browser with appropriate settings
                _browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Headless = true,
                    Args = new[]
                    {
                        "--no-sandbox",
                        "--disable-setuid-sandbox",
                        "--disable-dev-shm-usage",
                        "--disable-gpu",
                        "--no-first-run",
                        "--no-zygote",
                        "--single-process",
                        "--disable-web-security",
                        "--disable-features=VizDisplayCompositor"
                    }
                });

                Console.WriteLine("Browser launched successfully");
                _browserInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing browser: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                
                // Try alternative launch options
                try
                {
                    Console.WriteLine("Attempting alternative browser launch...");
                    _browser = await Puppeteer.LaunchAsync(new LaunchOptions
                    {
                        Headless = true,
                        Args = new[]
                        {
                            "--no-sandbox",
                            "--disable-setuid-sandbox"
                        }
                    });
                    
                    Console.WriteLine("Alternative browser launch successful");
                    _browserInitialized = true;
                }
                catch (Exception fallbackEx)
                {
                    Console.WriteLine($"Alternative browser launch also failed: {fallbackEx.Message}");
                    throw new InvalidOperationException("Failed to initialize PDF generation browser after multiple attempts", ex);
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _browser?.Dispose();
                _browser = null;
                _browserInitialized = false;
                _disposed = true;
            }
        }
    }
}

