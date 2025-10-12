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
                // Convert dates to UTC if they have a value
                var utcStartDate = startDate.HasValue ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc) : (DateTime?)null;
                var utcEndDate = endDate.HasValue ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc) : (DateTime?)null;

                _logger.LogInformation("Generating freelancer performance report for user {UserId} from {StartDate} to {EndDate}",
                    userId, utcStartDate?.ToString("yyyy-MM-dd") ?? "N/A", utcEndDate?.ToString("yyyy-MM-dd") ?? "N/A");

                var pdfBytes = await _reportService.GenerateFreelancerPerformanceReportAsync(userId, utcStartDate, utcEndDate);
                var fileName = $"FreelancerPerformanceReport_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

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
                // Convert dates to UTC if they have a value
                var utcStartDate = startDate.HasValue ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc) : (DateTime?)null;
                var utcEndDate = endDate.HasValue ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc) : (DateTime?)null;

                _logger.LogInformation("Generating client project report for user {UserId} from {StartDate} to {EndDate}",
                    userId, utcStartDate?.ToString("yyyy-MM-dd") ?? "N/A", utcEndDate?.ToString("yyyy-MM-dd") ?? "N/A");

                var pdfBytes = await _reportService.GenerateClientProjectReportAsync(userId, utcStartDate, utcEndDate);
                var fileName = $"ClientProjectReport_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

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
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SystemAnalytics(DateTime? startDate = null, DateTime? endDate = null)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            try
            {
                // Convert dates to UTC if they have a value
                var utcStartDate = startDate.HasValue ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc) : (DateTime?)null;
                var utcEndDate = endDate.HasValue ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc) : (DateTime?)null;

                _logger.LogInformation("Generating system analytics report from {StartDate} to {EndDate} by admin {UserId}",
                    utcStartDate?.ToString("yyyy-MM-dd") ?? "N/A", utcEndDate?.ToString("yyyy-MM-dd") ?? "N/A", userId ?? "Unknown");

                var pdfBytes = await _reportService.GenerateAdminSystemReportAsync(utcStartDate, utcEndDate);
                var fileName = $"SystemAnalyticsReport_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

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
                // Convert dates to UTC if they have a value
                var utcStartDate = startDate.HasValue ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc) : (DateTime?)null;
                var utcEndDate = endDate.HasValue ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc) : (DateTime?)null;

                _logger.LogInformation("Generating financial report for user {UserId} from {StartDate} to {EndDate}",
                    userId, utcStartDate?.ToString("yyyy-MM-dd") ?? "N/A", utcEndDate?.ToString("yyyy-MM-dd") ?? "N/A");

                var pdfBytes = await _reportService.GenerateFinancialReportAsync(userId, utcStartDate, utcEndDate);
                var fileName = $"FinancialReport_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

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