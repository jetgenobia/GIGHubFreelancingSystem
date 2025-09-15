using Microsoft.AspNetCore.Mvc;
using Freelancing.Services;
using Freelancing.Data;
using Microsoft.EntityFrameworkCore;

namespace Freelancing.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MLTrainingController : ControllerBase
    {
        private readonly ISmartHiringFeatureService _featureService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MLTrainingController> _logger;

        public MLTrainingController(ISmartHiringFeatureService featureService, ApplicationDbContext context, ILogger<MLTrainingController> logger)
        {
            _featureService = featureService;
            _context = context;
            _logger = logger;
        }

        [HttpGet("export-data")]
        public async Task<IActionResult> ExportTrainingData()
        {
            try
            {
                var fileName = $"smart_hiring_training_data_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var filePath = Path.Combine(Path.GetTempPath(), fileName);

                await _featureService.ExportTrainingDataToCsvAsync(filePath);

                var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                System.IO.File.Delete(filePath);

                return File(fileBytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting training data");
                return BadRequest($"Failed to export training data: {ex.Message}");
            }
        }

        [HttpGet("training-data-stats")]
        public async Task<IActionResult> GetTrainingDataStats()
        {
            try
            {
                var trainingData = await _featureService.PrepareTrainingDataAsync();

                return Ok(new
                {
                    TotalRecords = trainingData.Count,
                    SuccessfulMatches = trainingData.Count(t => t.IsSuccessfulMatch == 1),
                    UnsuccessfulMatches = trainingData.Count(t => t.IsSuccessfulMatch == 0),
                    MentorshipCompleted = trainingData.Count(t => t.MentorshipProgramCompleted == 1),
                    MentorshipNotCompleted = trainingData.Count(t => t.MentorshipProgramCompleted == 0),
                    Features = 17
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting training data stats");
                return BadRequest($"Failed to get training data statistics: {ex.Message}");
            }
        }

        // NEW: Debug endpoint to check your database data
        [HttpGet("debug-data")]
        public async Task<IActionResult> DebugDatabaseData()
        {
            try
            {
                var totalProjects = await _context.Projects.CountAsync();
                var projectsWithAcceptedBids = await _context.Projects
                    .Where(p => p.AcceptedBidId.HasValue)
                    .CountAsync();
                var completedProjects = await _context.Projects
                    .Where(p => p.Status == "Completed")
                    .CountAsync();
                var completedWithBids = await _context.Projects
                    .Where(p => p.AcceptedBidId.HasValue && p.Status == "Completed")
                    .CountAsync();
                var totalBiddings = await _context.Biddings.CountAsync();
                var acceptedBiddings = await _context.Biddings
                    .Where(b => b.IsAccepted)
                    .CountAsync();
                var totalFeedbacks = await _context.FreelancerFeedbacks.CountAsync();

                // Get sample project statuses
                var projectStatuses = await _context.Projects
                    .GroupBy(p => p.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync();

                return Ok(new
                {
                    DatabaseSummary = new
                    {
                        TotalProjects = totalProjects,
                        ProjectsWithAcceptedBids = projectsWithAcceptedBids,
                        CompletedProjects = completedProjects,
                        CompletedProjectsWithBids = completedWithBids,
                        TotalBiddings = totalBiddings,
                        AcceptedBiddings = acceptedBiddings,
                        TotalFeedbacks = totalFeedbacks
                    },
                    ProjectStatusBreakdown = projectStatuses,
                    TrainingDataRequirement = "Need projects with Status='Completed' AND AcceptedBidId is not null"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting debug data");
                return BadRequest($"Failed to get debug data: {ex.Message}");
            }
        }
    }
}