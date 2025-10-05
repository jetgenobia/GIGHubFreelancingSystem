using Freelancing.Data;
using Freelancing.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text;

namespace Freelancing.Services
{
    public interface ISmartHiringService
    {
        Task<List<SmartHiringPrediction>> GetBestFreelancersAsync(Guid projectId);
        Task<SmartHiringPrediction> GetFreelancerScoreAsync(Guid projectId, string freelancerId);
        Task RecordHiringOutcomeAsync(Guid projectId, string freelancerId, bool wasSuccessful);
        Task<SmartHiringInsights> GetProjectInsightsAsync(Guid projectId);
        void ClearPredictionCache(); // Add cache clearing method
        string GetCacheStatus(); // Add cache status method for debugging
    }

    public class SmartHiringService : ISmartHiringService
    {
        private readonly ApplicationDbContext _context;
        private readonly ISmartHiringFeatureService _featureService;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SmartHiringService> _logger;
        private readonly ILocalRandomForestService _localRandomForest;
        
        // Static cache for predictions to persist across service instances
        private static readonly Dictionary<string, float> _predictionCache = new();
        private static readonly object _cacheLock = new object();

        public SmartHiringService(
            ApplicationDbContext context,
            ISmartHiringFeatureService featureService,
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<SmartHiringService> logger,
            ILocalRandomForestService localRandomForest)
        {
            _context = context;
            _featureService = featureService;
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _localRandomForest = localRandomForest;
        }

        public async Task<List<SmartHiringPrediction>> GetBestFreelancersAsync(Guid projectId)
        {
            try
            {
                // Get all bidders for this project
                var bidders = await _context.Biddings
                    .Where(b => b.ProjectId == projectId)
                    .Include(b => b.User)
                    .Include(b => b.Project)
                    .ToListAsync();

                if (!bidders.Any())
                {
                    _logger.LogWarning($"No bidders found for project {projectId}");
                    return new List<SmartHiringPrediction>();
                }

                var predictions = new List<SmartHiringPrediction>();

                foreach (var bid in bidders)
                {
                    try
                    {
                        var prediction = await GetFreelancerScoreAsync(projectId, bid.UserId);
                        prediction.BidId = bid.Id;
                        prediction.BidAmount = bid.Budget;
                        prediction.ProposedDelivery = bid.Delivery;
                        prediction.Proposal = bid.Proposal;
                        prediction.PreviousWorksPaths = bid.PreviousWorksPaths;
                        prediction.RepositoryLinks = bid.RepositoryLinks;

                        predictions.Add(prediction);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error calculating score for freelancer {bid.UserId} on project {projectId}");
                        
                        // Add default prediction to avoid missing bidders
                        predictions.Add(new SmartHiringPrediction
                        {
                            FreelancerId = bid.UserId.ToString(),
                            FreelancerName = $"{bid.User.FirstName} {bid.User.LastName}",
                            MatchScore = 0.5f,
                            Confidence = 0.1f,
                            Reasoning = "Unable to calculate score - insufficient data",
                            BidId = bid.Id,
                            BidAmount = bid.Budget,
                            ProposedDelivery = bid.Delivery,
                            Proposal = bid.Proposal,
                            PreviousWorksPaths = bid.PreviousWorksPaths,
                            RepositoryLinks = bid.RepositoryLinks
                        });
                    }
                }

                // Sort by match score (highest first)
                return predictions.OrderByDescending(p => p.MatchScore).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting best freelancers for project {projectId}");
                throw;
            }
        }

        public async Task<SmartHiringPrediction> GetFreelancerScoreAsync(Guid projectId, string freelancerId)
        {
            try
            {
                // Extract features
                var features = await _featureService.ExtractFeaturesAsync(projectId, freelancerId);

                // Get freelancer details
                var freelancer = await _context.UserAccounts
                    .FirstOrDefaultAsync(u => u.Id == freelancerId);

                if (freelancer == null)
                    throw new ArgumentException("Freelancer not found");

                // Use local Random Forest model (fallback to rule-based if not available)
                var score = await UseLocalRandomForestAsync(features, projectId, freelancerId);

                // Generate reasoning
                var reasoning = GenerateReasoning(features, score);

                // Calculate confidence based on data completeness
                var confidence = CalculateConfidence(features);

                return new SmartHiringPrediction
                {
                    FreelancerId = freelancerId.ToString(),
                    FreelancerName = $"{freelancer.FirstName} {freelancer.LastName}",
                    FreelancerPhoto = freelancer.Photo,
                    MatchScore = score,
                    Confidence = confidence,
                    Reasoning = reasoning,
                    Features = features,
                    KeyStrengths = IdentifyKeyStrengths(features),
                    PotentialConcerns = IdentifyPotentialConcerns(features)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error calculating score for freelancer {freelancerId} on project {projectId}");
                throw;
            }
        }

        private async Task<float> UseLocalRandomForestAsync(MLFeatures features, Guid projectId, string freelancerId)
        {
            try
            {
                // ✅ FORCE INITIALIZATION - This will detect the newly trained model
                await _localRandomForest.EnsureInitializedAsync();

                // Check if local Random Forest is available
                if (_localRandomForest.IsAvailable)
                {
                    // Create cache key for this project-freelancer combination
                    var cacheKey = $"{projectId}_{freelancerId}";
                    _logger.LogInformation($"Cache key: {cacheKey}");
                    _logger.LogInformation($"Static cache size: {_predictionCache.Count}");

                    // Check cache first
                    lock (_cacheLock)
                    {
                        if (_predictionCache.ContainsKey(cacheKey))
                        {
                            var cachedPrediction = _predictionCache[cacheKey];
                            _logger.LogInformation($"CACHE HIT! Using cached Random Forest prediction: {cachedPrediction:F3}");
                            return cachedPrediction;
                        }
                        else
                        {
                            _logger.LogInformation($"CACHE MISS! No cached prediction found for key: {cacheKey}");
                        }
                    }

                    _logger.LogInformation("Using Random Forest model for prediction");

                    // Convert MLFeatures to dictionary for Random Forest
                    var featureDict = new Dictionary<string, object>
                    {
                        ["skill_match_score"] = features.SkillMatchScore,
                        ["avg_rating"] = features.AvgRating,
                        ["recommendation_rate"] = features.RecommendationRate,
                        ["completion_rate"] = features.CompletionRate,
                        ["bid_success_rate"] = features.BidSuccessRate,
                        ["category_experience"] = features.CategoryExperience,
                        ["response_time_hours"] = features.ResponseTimeHours,
                        ["portfolio_quality"] = features.PortfolioQuality,
                        ["budget_match_score"] = features.BudgetMatchScore,
                        ["delivery_time_days"] = features.DeliveryTimeDays,
                        ["freelancer_tenure_days"] = features.FreelancerTenureDays,
                        ["project_complexity"] = features.ProjectComplexity,
                        ["client_history_score"] = features.ClientHistoryScore,
                        ["past_collaboration"] = features.PastCollaboration,
                        ["skills_count_match"] = features.SkillsCountMatch,
                        ["workload_factor"] = features.WorkloadFactor,
                        ["mentorship_program_completed"] = features.MentorshipProgramCompleted
                    };

                    // Get prediction from local Random Forest
                    var prediction = await _localRandomForest.PredictAsync(featureDict);

                    // Cache the result
                    lock (_cacheLock)
                    {
                        _predictionCache[cacheKey] = prediction;
                        _logger.LogInformation($"Cached prediction for key: {cacheKey}, value: {prediction:F3}");
                        _logger.LogInformation($"Static cache size after caching: {_predictionCache.Count}");
                    }

                    _logger.LogInformation($"Random Forest prediction: {prediction:F3}");
                    return prediction;
                }
                else
                {
                    _logger.LogWarning("Random Forest not available, using fallback scoring");
                    return CalculateFallbackScore(features);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error using Random Forest, falling back to rule-based scoring");
                return CalculateFallbackScore(features);
            }
        }

        private async Task<float> CallAzureMLEndpointAsync(MLFeatures features)
        {
            try
            {
                var endpoint = _configuration["AzureML:ScoringEndpoint"];
                var apiKey = _configuration["AzureML:ApiKey"];

                if (string.IsNullOrEmpty(endpoint))
                {
                    _logger.LogWarning("Azure ML endpoint not configured, using fallback scoring");
                    return CalculateFallbackScore(features);
                }

                // Prepare the data for the ML model
                var inputData = new
                {
                    data = new[]
                    {
                        new[]
                        {
                            features.SkillMatchScore,
                            features.AvgRating,
                            features.RecommendationRate,
                            features.CompletionRate,
                            features.BidSuccessRate,
                            features.CategoryExperience,
                            features.ResponseTimeHours,
                            features.PortfolioQuality,
                            features.BudgetMatchScore,
                            features.DeliveryTimeDays,
                            features.FreelancerTenureDays,
                            features.ProjectComplexity,
                            features.ClientHistoryScore,
                            features.PastCollaboration,
                            features.SkillsCountMatch,
                            features.WorkloadFactor
                        }
                    }
                };

                var json = JsonSerializer.Serialize(inputData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Set headers
                _httpClient.DefaultRequestHeaders.Clear();
                if (!string.IsNullOrEmpty(apiKey))
                {
                    _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                }

                var response = await _httpClient.PostAsync(endpoint, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<MLPredictionResponse>(responseContent);
                    
                    return result?.Predictions?.FirstOrDefault() ?? 0.5f;
                }
                else
                {
                    _logger.LogError($"Azure ML API call failed: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                    return CalculateFallbackScore(features);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Azure ML endpoint");
                return CalculateFallbackScore(features);
            }
        }

        private float CalculateFallbackScore(MLFeatures features)
        {
            // Simple weighted scoring as fallback when ML model is unavailable
            float score = 0;

            score += features.SkillMatchScore * 0.23f;        // 23% weight (reduced)
            score += (features.AvgRating / 5.0f) * 0.18f;    // 18% weight (reduced)
            score += features.RecommendationRate * 0.14f;     // 14% weight (reduced)
            score += features.CompletionRate * 0.14f;         // 14% weight (reduced)
            score += features.BidSuccessRate * 0.09f;         // 9% weight (reduced)
            score += (features.CategoryExperience / 10.0f) * 0.09f; // 9% weight (reduced)
            score += features.BudgetMatchScore * 0.05f;       // 5% weight
            score += features.MentorshipProgramCompleted * 0.08f; // 8% weight for mentorship

            return Math.Min(Math.Max(score, 0), 1); // Clamp between 0 and 1
        }

        private string GenerateReasoning(MLFeatures features, float score)
        {
            var reasons = new List<string>();

            if (features.SkillMatchScore > 0.8f)
                reasons.Add($"Excellent skill match ({features.SkillMatchScore:P0})");
            else if (features.SkillMatchScore > 0.6f)
                reasons.Add($"Good skill match ({features.SkillMatchScore:P0})");
            else if (features.SkillMatchScore < 0.4f)
                reasons.Add($"Limited skill match ({features.SkillMatchScore:P0})");

            if (features.AvgRating >= 4.5f)
                reasons.Add($"Outstanding ratings ({features.AvgRating:F1}/5.0)");
            else if (features.AvgRating >= 4.0f)
                reasons.Add($"Strong ratings ({features.AvgRating:F1}/5.0)");

            if (features.CompletionRate > 0.9f)
                reasons.Add($"Excellent completion rate ({features.CompletionRate:P0})");
            else if (features.CompletionRate < 0.7f)
                reasons.Add($"Concerning completion rate ({features.CompletionRate:P0})");

            if (features.CategoryExperience > 5)
                reasons.Add($"Extensive experience in this category ({features.CategoryExperience} projects)");
            else if (features.CategoryExperience == 0)
                reasons.Add("New to this project category");

            if (features.PastCollaboration == 1)
                reasons.Add("Has successfully worked with you before");

            if (features.MentorshipProgramCompleted == 1)
                reasons.Add("Completed GIGBuddies (Mentorship) program");

            if (features.WorkloadFactor > 0.8f)
                reasons.Add("Currently has high workload");

            if (!reasons.Any())
                reasons.Add("Balanced profile across all criteria");

            return string.Join(", ", reasons);
        }

        private float CalculateConfidence(MLFeatures features)
        {
            float confidence = 1.0f;

            // Reduce confidence for new freelancers
            if (features.AvgRating == 3.0f) confidence -= 0.2f; // Default rating indicates new freelancer
            if (features.CategoryExperience == 0) confidence -= 0.1f;
            if (features.FreelancerTenureDays < 30) confidence -= 0.1f;

            // Increase confidence for established freelancers
            if (features.AvgRating > 4.0f && features.CategoryExperience > 3) confidence += 0.1f;

            return Math.Min(Math.Max(confidence, 0.1f), 1.0f);
        }

        private List<string> IdentifyKeyStrengths(MLFeatures features)
        {
            var strengths = new List<string>();

            if (features.SkillMatchScore > 0.8f)
                strengths.Add("Perfect skill alignment");

            if (features.AvgRating >= 4.5f)
                strengths.Add("Exceptional client satisfaction");

            if (features.CompletionRate > 0.9f)
                strengths.Add("Reliable project completion");

            if (features.CategoryExperience > 5)
                strengths.Add("Deep category expertise");

            if (features.BudgetMatchScore > 0.9f)
                strengths.Add("Competitive pricing");

            if (features.PastCollaboration == 1)
                strengths.Add("Proven collaboration history");

            if (features.MentorshipProgramCompleted == 1)
                strengths.Add("Mentorship for career growth");

            return strengths.Take(3).ToList();
        }

        private List<string> IdentifyPotentialConcerns(MLFeatures features)
        {
            var concerns = new List<string>();

            if (features.SkillMatchScore < 0.5f)
                concerns.Add("Limited skill match");

            if (features.CompletionRate < 0.8f)
                concerns.Add("Inconsistent project completion");

            if (features.WorkloadFactor > 0.8f)
                concerns.Add("High current workload");

            if (features.CategoryExperience == 0)
                concerns.Add("No experience in this category");

            if (features.ResponseTimeHours > 48)
                concerns.Add("Slow response time");

            return concerns.Take(2).ToList(); // Limit to top 2
        }

        public async Task RecordHiringOutcomeAsync(Guid projectId, string freelancerId, bool wasSuccessful)
        {
            try
            {
                var outcome = new HiringOutcome
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    FreelancerId = freelancerId.ToString(),
                    WasSuccessful = wasSuccessful,
                    RecordedAt = DateTime.UtcNow
                };

                _context.HiringOutcomes.Add(outcome);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Recorded hiring outcome for project {projectId}, freelancer {freelancerId}: {wasSuccessful}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error recording hiring outcome for project {projectId}, freelancer {freelancerId}");
            }
        }

        public async Task<SmartHiringInsights> GetProjectInsightsAsync(Guid projectId)
        {
            var project = await _context.Projects
                .Include(p => p.Biddings)
                .Include(p => p.ProjectSkills)
                .ThenInclude(ps => ps.UserSkill)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
                throw new ArgumentException("Project not found");

            var bidCount = project.Biddings?.Count ?? 0;
            var avgBudget = project.Biddings?.Any() == true ? project.Biddings.Average(b => b.Budget) : 0;

            // Calculate skill availability
            var requiredSkills = project.ProjectSkills?.Select(ps => ps.UserSkillId).ToList() ?? new List<Guid>();
            var freelancersWithSkills = 0;

            if (requiredSkills.Any())
            {
                freelancersWithSkills = await _context.UserAccountSkills
                    .Where(uas => requiredSkills.Contains(uas.UserSkillId))
                    .Select(uas => uas.UserAccountId)
                    .Distinct()
                    .CountAsync();
            }

            return new SmartHiringInsights
            {
                ProjectId = projectId,
                TotalBidders = bidCount,
                AverageBidAmount = avgBudget,
                SkillAvailability = freelancersWithSkills,
                RecommendedBudgetRange = $"${avgBudget * 0.9:F0} - ${avgBudget * 1.1:F0}",
                CompetitionLevel = bidCount > 10 ? "High" : bidCount > 5 ? "Medium" : "Low",
                EstimatedHiringTime = bidCount > 5 ? "1-2 days" : "3-5 days"
            };
        }

        public void ClearPredictionCache()
        {
            lock (_cacheLock)
            {
                _predictionCache.Clear();
                _logger.LogInformation("Prediction cache cleared.");
            }
        }

        public string GetCacheStatus()
        {
            lock (_cacheLock)
            {
                return $"Cache size: {_predictionCache.Count}";
            }
        }
    }

    // Supporting classes
    public class SmartHiringPrediction
    {
        public string FreelancerId { get; set; }
        public string FreelancerName { get; set; } = string.Empty;
        public string? FreelancerPhoto { get; set; }
        public float MatchScore { get; set; }
        public float Confidence { get; set; }
        public string Reasoning { get; set; } = string.Empty;
        public MLFeatures? Features { get; set; }
        public List<string> KeyStrengths { get; set; } = new();
        public List<string> PotentialConcerns { get; set; } = new();
        
        // Bidding information
        public Guid BidId { get; set; }
        public int BidAmount { get; set; }
        public string ProposedDelivery { get; set; } = string.Empty;
        public string Proposal { get; set; } = string.Empty;
        public string? PreviousWorksPaths { get; set; }
        public string? RepositoryLinks { get; set; }
    }

    public class SmartHiringInsights
    {
        public Guid ProjectId { get; set; }
        public int TotalBidders { get; set; }
        public double AverageBidAmount { get; set; }
        public int SkillAvailability { get; set; }
        public string RecommendedBudgetRange { get; set; } = string.Empty;
        public string CompetitionLevel { get; set; } = string.Empty;
        public string EstimatedHiringTime { get; set; } = string.Empty;
    }

    public class MLPredictionResponse
    {
        public List<float>? Predictions { get; set; }
        public string? ModelVersion { get; set; }
    }

    // Entity for tracking hiring outcomes
    public class HiringOutcome
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string FreelancerId { get; set; }
        public bool WasSuccessful { get; set; }
        public DateTime RecordedAt { get; set; }
    }
}

