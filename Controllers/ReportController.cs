using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Freelancing.Services;
using System.Security.Claims;

namespace Freelancing.Controllers
{
    [Authorize]
    public class ReportController : Controller
    {
        private readonly IReportService _reportService;
        private readonly ILogger<ReportController> _logger;

        public ReportController(IReportService reportService, ILogger<ReportController> logger)
        {
            _reportService = reportService;
            _logger = logger;
        }

        [HttpGet]
        [Authorize(Roles = "Freelancer")]
        public async Task<IActionResult> FreelancerPerformance(DateTime? startDate = null, DateTime? endDate = null)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("FreelancerPerformance: User ID not found in claims");
                return Unauthorized();
            }

            try
            {
                _logger.LogInformation("Generating freelancer performance report for user {UserId} from {StartDate} to {EndDate}",
                    userId, startDate?.ToString("yyyy-MM-dd") ?? "N/A", endDate?.ToString("yyyy-MM-dd") ?? "N/A");

                var pdfBytes = await _reportService.GenerateFreelancerPerformanceReportAsync(userId, startDate, endDate);
                var fileName = $"FreelancerPerformanceReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

                _logger.LogInformation("Successfully generated freelancer performance report for user {UserId}", userId);
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating freelancer performance report for user {UserId}", userId);
                return BadRequest("Error generating report. Please try again.");
            }
        }

        [HttpGet]
        [Authorize(Roles = "Freelancer")]
        public async Task<IActionResult> FreelancerBids(DateTime? startDate = null, DateTime? endDate = null)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("FreelancerBids: User ID not found in claims");
                return Unauthorized();
            }

            try
            {
                _logger.LogInformation("Generating bids report for user {UserId} from {StartDate} to {EndDate}",
                    userId, startDate?.ToString("yyyy-MM-dd") ?? "N/A", endDate?.ToString("yyyy-MM-dd") ?? "N/A");

                var pdfBytes = await _reportService.GenerateFreelancerBidsReportAsync(userId, startDate, endDate);
                var fileName = $"BidsReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

                _logger.LogInformation("Successfully generated bids report for user {UserId}", userId);
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating bids report for user {UserId}", userId);
                return BadRequest("Error generating report. Please try again.");
            }
        }

        [HttpGet]
        [Authorize(Roles = "Freelancer")]
        public async Task<IActionResult> FreelancerReviews(DateTime? startDate = null, DateTime? endDate = null)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("FreelancerReviews: User ID not found in claims");
                return Unauthorized();
            }

            try
            {
                _logger.LogInformation("Generating reviews report for user {UserId} from {StartDate} to {EndDate}",
                    userId, startDate?.ToString("yyyy-MM-dd") ?? "N/A", endDate?.ToString("yyyy-MM-dd") ?? "N/A");

                var pdfBytes = await _reportService.GenerateFreelancerReviewsReportAsync(userId, startDate, endDate);
                var fileName = $"ReviewsReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

                _logger.LogInformation("Successfully generated reviews report for user {UserId}", userId);
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating reviews report for user {UserId}", userId);
                return BadRequest("Error generating report. Please try again.");
            }
        }

        [HttpGet]
        [Authorize(Roles = "Freelancer")]
        public async Task<IActionResult> FreelancerFinancial(DateTime? startDate = null, DateTime? endDate = null)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("FreelancerFinancial: User ID not found in claims");
                return Unauthorized();
            }

            try
            {
                _logger.LogInformation("Generating financial report for user {UserId} from {StartDate} to {EndDate}",
                    userId, startDate?.ToString("yyyy-MM-dd") ?? "N/A", endDate?.ToString("yyyy-MM-dd") ?? "N/A");

                var pdfBytes = await _reportService.GenerateFreelancerFinancialReportAsync(userId, startDate, endDate);
                var fileName = $"FinancialReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

                _logger.LogInformation("Successfully generated financial report for user {UserId}", userId);
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating financial report for user {UserId}", userId);
                return BadRequest("Error generating report. Please try again.");
            }
        }

        [HttpGet]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> ClientProjects(DateTime? startDate = null, DateTime? endDate = null)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("ClientProjects: User ID not found in claims");
                return Unauthorized();
            }

            try
            {
                _logger.LogInformation("Generating client project report for user {UserId} from {StartDate} to {EndDate}",
                    userId, startDate?.ToString("yyyy-MM-dd") ?? "N/A", endDate?.ToString("yyyy-MM-dd") ?? "N/A");

                var pdfBytes = await _reportService.GenerateClientProjectReportAsync(userId, startDate, endDate);
                var fileName = $"ClientProjectReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

                _logger.LogInformation("Successfully generated client project report for user {UserId}", userId);
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating client project report for user {UserId}", userId);
                return BadRequest("Error generating report. Please try again.");
            }
        }

        [HttpGet]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> ClientProjectsCompletion(DateTime? startDate = null, DateTime? endDate = null)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("ClientProjectsCompletion: User ID not found in claims");
                return Unauthorized();
            }

            try
            {
                _logger.LogInformation("Generating projects completion report for user {UserId} from {StartDate} to {EndDate}",
                    userId, startDate?.ToString("yyyy-MM-dd") ?? "N/A", endDate?.ToString("yyyy-MM-dd") ?? "N/A");

                var pdfBytes = await _reportService.GenerateClientProjectsCompletionReportAsync(userId, startDate, endDate);
                var fileName = $"ProjectsCompletionReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

                _logger.LogInformation("Successfully generated projects completion report for user {UserId}", userId);
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating projects completion report for user {UserId}", userId);
                return BadRequest("Error generating report. Please try again.");
            }
        }

        [HttpGet]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> ClientReviews(DateTime? startDate = null, DateTime? endDate = null)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("ClientReviews: User ID not found in claims");
                return Unauthorized();
            }

            try
            {
                _logger.LogInformation("Generating reviews report for user {UserId} from {StartDate} to {EndDate}",
                    userId, startDate?.ToString("yyyy-MM-dd") ?? "N/A", endDate?.ToString("yyyy-MM-dd") ?? "N/A");

                var pdfBytes = await _reportService.GenerateClientReviewsReportAsync(userId, startDate, endDate);
                var fileName = $"ReviewsReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

                _logger.LogInformation("Successfully generated reviews report for user {UserId}", userId);
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating reviews report for user {UserId}", userId);
                return BadRequest("Error generating report. Please try again.");
            }
        }

        [HttpGet]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> ClientFinancial(DateTime? startDate = null, DateTime? endDate = null)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("ClientFinancial: User ID not found in claims");
                return Unauthorized();
            }

            try
            {
                _logger.LogInformation("Generating financial report for user {UserId} from {StartDate} to {EndDate}",
                    userId, startDate?.ToString("yyyy-MM-dd") ?? "N/A", endDate?.ToString("yyyy-MM-dd") ?? "N/A");

                var pdfBytes = await _reportService.GenerateClientFinancialReportAsync(userId, startDate, endDate);
                var fileName = $"FinancialReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

                _logger.LogInformation("Successfully generated financial report for user {UserId}", userId);
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating financial report for user {UserId}", userId);
                return BadRequest("Error generating report. Please try again.");
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SystemAnalytics(DateTime? startDate = null, DateTime? endDate = null)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            try
            {
                _logger.LogInformation("Generating system analytics report from {StartDate} to {EndDate} by admin {UserId}",
                    startDate?.ToString("yyyy-MM-dd") ?? "N/A", endDate?.ToString("yyyy-MM-dd") ?? "N/A", userId ?? "Unknown");

                var pdfBytes = await _reportService.GenerateAdminSystemReportAsync(startDate, endDate);
                var fileName = $"SystemAnalyticsReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

                _logger.LogInformation("Successfully generated system analytics report by admin {UserId}", userId ?? "Unknown");
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating system analytics report by admin {UserId}", userId ?? "Unknown");
                return BadRequest("Error generating report. Please try again.");
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminProjectsOverview(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                _logger.LogInformation("Generating admin projects overview report from {StartDate} to {EndDate}",
                    startDate?.ToString("yyyy-MM-dd") ?? "N/A", endDate?.ToString("yyyy-MM-dd") ?? "N/A");

                var pdfBytes = await _reportService.GenerateAdminProjectsOverviewReportAsync(startDate, endDate);
                var fileName = $"AdminProjectsOverview_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

                _logger.LogInformation("Successfully generated admin projects overview report");
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating admin projects overview report");
                return BadRequest("Error generating report. Please try again.");
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminProjectDelivery(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                _logger.LogInformation("Generating admin project delivery report from {StartDate} to {EndDate}",
                    startDate?.ToString("yyyy-MM-dd") ?? "N/A", endDate?.ToString("yyyy-MM-dd") ?? "N/A");

                var pdfBytes = await _reportService.GenerateAdminProjectDeliveryReportAsync(startDate, endDate);
                var fileName = $"AdminProjectDelivery_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

                _logger.LogInformation("Successfully generated admin project delivery report");
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating admin project delivery report");
                return BadRequest("Error generating report. Please try again.");
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminUsersOverview(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                _logger.LogInformation("Generating admin users overview report from {StartDate} to {EndDate}",
                    startDate?.ToString("yyyy-MM-dd") ?? "N/A", endDate?.ToString("yyyy-MM-dd") ?? "N/A");

                var pdfBytes = await _reportService.GenerateAdminUsersOverviewReportAsync(startDate, endDate);
                var fileName = $"AdminUsersOverview_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

                _logger.LogInformation("Successfully generated admin users overview report");
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating admin users overview report");
                return BadRequest("Error generating report. Please try again.");
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminFinancial(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                _logger.LogInformation("Generating admin financial report from {StartDate} to {EndDate}",
                    startDate?.ToString("yyyy-MM-dd") ?? "N/A", endDate?.ToString("yyyy-MM-dd") ?? "N/A");

                var pdfBytes = await _reportService.GenerateAdminFinancialReportAsync(startDate, endDate);
                var fileName = $"AdminFinancial_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

                _logger.LogInformation("Successfully generated admin financial report");
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating admin financial report");
                return BadRequest("Error generating report. Please try again.");
            }
        }

        [HttpGet]
        [Authorize(Roles = "Freelancer")]
        public async Task<IActionResult> Financial(DateTime? startDate = null, DateTime? endDate = null)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Financial: User ID not found in claims");
                return Unauthorized();
            }

            try
            {
                _logger.LogInformation("Generating financial report for user {UserId} from {StartDate} to {EndDate}",
                    userId, startDate?.ToString("yyyy-MM-dd") ?? "N/A", endDate?.ToString("yyyy-MM-dd") ?? "N/A");

                var pdfBytes = await _reportService.GenerateFinancialReportAsync(userId, startDate, endDate);
                var fileName = $"FinancialReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

                _logger.LogInformation("Successfully generated financial report for user {UserId}", userId);
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating financial report for user {UserId}", userId);
                return BadRequest("Error generating report. Please try again.");
            }
        }
    }
}