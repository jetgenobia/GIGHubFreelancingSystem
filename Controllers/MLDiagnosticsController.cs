using Microsoft.AspNetCore.Mvc;
using Freelancing.Services;
using Microsoft.AspNetCore.Authorization;
using Freelancing.Models.ViewModels;

namespace Freelancing.Controllers
{
    [Authorize(Roles = "Admin")]
    public class MLDiagnosticsController : Controller
    {
        private readonly ILocalRandomForestService _randomForestService;
        private readonly ISmartHiringService _smartHiringService;
        private readonly ILogger<MLDiagnosticsController> _logger;

        public MLDiagnosticsController(
            ILocalRandomForestService randomForestService,
            ISmartHiringService smartHiringService,
            ILogger<MLDiagnosticsController> logger)
        {
            _randomForestService = randomForestService;
            _smartHiringService = smartHiringService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var model = new MLDiagnosticsViewModel
            {
                IsRandomForestAvailable = _randomForestService.IsAvailable,
                CacheStatus = _smartHiringService.GetCacheStatus(),
                FlaskApiUrl = Environment.GetEnvironmentVariable("FLASK_API_URL") ?? "Not configured",
                IsRailwayEnvironment = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RAILWAY_ENVIRONMENT")),
                EnvironmentVariables = GetRelevantEnvironmentVariables()
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> TestConnection()
        {
            try
            {
                await _randomForestService.EnsureInitializedAsync();
                
                if (_randomForestService.IsAvailable)
                {
                    // Test with sample data
                    var testFeatures = new Dictionary<string, object>
                    {
                        ["skill_match_score"] = 0.8,
                        ["avg_rating"] = 4.5,
                        ["recommendation_rate"] = 0.9,
                        ["completion_rate"] = 0.95,
                        ["bid_success_rate"] = 0.7,
                        ["category_experience"] = 5,
                        ["response_time_hours"] = 2,
                        ["portfolio_quality"] = 0.8,
                        ["budget_match_score"] = 0.9,
                        ["delivery_time_days"] = 7,
                        ["freelancer_tenure_days"] = 365,
                        ["project_complexity"] = 0.6,
                        ["client_history_score"] = 0.8,
                        ["past_collaboration"] = 1,
                        ["skills_count_match"] = 0.9,
                        ["workload_factor"] = 0.3,
                        ["mentorship_program_completed"] = 1
                    };

                    var prediction = await _randomForestService.PredictAsync(testFeatures);
                    
                    TempData["SuccessMessage"] = $"? Connection successful! Test prediction: {prediction:F3}";
                }
                else
                {
                    TempData["ErrorMessage"] = "? Random Forest service is not available";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing ML connection");
                TempData["ErrorMessage"] = $"? Connection failed: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult ClearCache()
        {
            try
            {
                _smartHiringService.ClearPredictionCache();
                TempData["SuccessMessage"] = "? Prediction cache cleared successfully";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache");
                TempData["ErrorMessage"] = $"? Failed to clear cache: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        private Dictionary<string, string> GetRelevantEnvironmentVariables()
        {
            var relevantVars = new[] 
            {
                "FLASK_API_URL",
                "RAILWAY_ENVIRONMENT",
                "PORT",
                "HOST",
                "FLASK_ENV",
                "PYTHONPATH"
            };

            var result = new Dictionary<string, string>();
            foreach (var varName in relevantVars)
            {
                result[varName] = Environment.GetEnvironmentVariable(varName) ?? "Not set";
            }

            return result;
        }
    }
}