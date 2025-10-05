using System.Text.Json;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace Freelancing.Services
{
    public interface ILocalRandomForestService
    {
        Task<float> PredictAsync(Dictionary<string, object> features);
        bool IsAvailable { get; }
        Task EnsureInitializedAsync();
        Task<string> TrainModelAsync(); // Add training method
    }

    public class LocalRandomForestService : ILocalRandomForestService
    {
        private readonly ILogger<LocalRandomForestService> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ISmartHiringFeatureService _featureService;
        private MLContext _mlContext;
        private ITransformer _model;
        private PredictionEngine<SmartHiringInput, SmartHiringOutput> _predictionEngine;
        private bool _isInitialized = false;
        private readonly object _initLock = new object();

        public LocalRandomForestService(
            ILogger<LocalRandomForestService> logger,
            IWebHostEnvironment webHostEnvironment,
            ISmartHiringFeatureService featureService)
        {
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
            _featureService = featureService;
            _mlContext = new MLContext(seed: 0);
        }

        public bool IsAvailable => _isInitialized;

        public async Task EnsureInitializedAsync()
        {
            if (_isInitialized) return;

            await Task.Run(() =>
            {
                lock (_initLock)
                {
                    if (_isInitialized) return;

                    try
                    {
                        var modelPath = Path.Combine(_webHostEnvironment.WebRootPath, "models", "smart_hiring_model.zip");

                        if (File.Exists(modelPath))
                        {
                            _logger.LogInformation("Loading ML.NET model from: {ModelPath}", modelPath);
                            _model = _mlContext.Model.Load(modelPath, out var modelInputSchema);
                            _predictionEngine = _mlContext.Model.CreatePredictionEngine<SmartHiringInput, SmartHiringOutput>(_model);
                            _isInitialized = true;
                            _logger.LogInformation("ML.NET Random Forest model loaded successfully");
                        }
                        else
                        {
                            _logger.LogWarning("ML.NET model file not found at: {ModelPath}. You can train a new model or use fallback scoring.", modelPath);
                            // Still mark as initialized to use fallback
                            _isInitialized = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to load ML.NET model. Using fallback scoring.");
                        // Still mark as initialized to use fallback
                        _isInitialized = true;
                    }
                }
            });
        }

        public async Task<float> PredictAsync(Dictionary<string, object> features)
        {
            if (!_isInitialized)
            {
                await EnsureInitializedAsync();
            }

            try
            {
                if (_predictionEngine != null)
                {
                    return await PredictWithMLNetAsync(features);
                }
                else
                {
                    _logger.LogDebug("Using fallback scoring - ML model not available");
                    return CalculateFallbackScore(features);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ML.NET prediction failed, using fallback scoring");
                return CalculateFallbackScore(features);
            }
        }

        private async Task<float> PredictWithMLNetAsync(Dictionary<string, object> features)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Convert features to ML.NET input format
                    var input = new SmartHiringInput
                    {
                        SkillMatchScore = Convert.ToSingle(features.GetValueOrDefault("skill_match_score", 0f)),
                        AvgRating = Convert.ToSingle(features.GetValueOrDefault("avg_rating", 3.0f)),
                        RecommendationRate = Convert.ToSingle(features.GetValueOrDefault("recommendation_rate", 0.5f)),
                        CompletionRate = Convert.ToSingle(features.GetValueOrDefault("completion_rate", 0.8f)),
                        BidSuccessRate = Convert.ToSingle(features.GetValueOrDefault("bid_success_rate", 0.1f)),
                        CategoryExperience = Convert.ToSingle(features.GetValueOrDefault("category_experience", 0)),
                        ResponseTimeHours = Convert.ToSingle(features.GetValueOrDefault("response_time_hours", 24f)),
                        PortfolioQuality = Convert.ToSingle(features.GetValueOrDefault("portfolio_quality", 5f)),
                        BudgetMatchScore = Convert.ToSingle(features.GetValueOrDefault("budget_match_score", 0.5f)),
                        DeliveryTimeDays = Convert.ToSingle(features.GetValueOrDefault("delivery_time_days", 7f)),
                        FreelancerTenureDays = Convert.ToSingle(features.GetValueOrDefault("freelancer_tenure_days", 365f)),
                        ProjectComplexity = Convert.ToSingle(features.GetValueOrDefault("project_complexity", 5f)),
                        ClientHistoryScore = Convert.ToSingle(features.GetValueOrDefault("client_history_score", 0.5f)),
                        PastCollaboration = Convert.ToSingle(features.GetValueOrDefault("past_collaboration", 0)),
                        SkillsCountMatch = Convert.ToSingle(features.GetValueOrDefault("skills_count_match", 0)),
                        WorkloadFactor = Convert.ToSingle(features.GetValueOrDefault("workload_factor", 0.5f)),
                        MentorshipProgramCompleted = Convert.ToSingle(features.GetValueOrDefault("mentorship_program_completed", 0))
                    };

                    // Make prediction
                    var prediction = _predictionEngine.Predict(input);

                    _logger.LogDebug("ML.NET prediction score: {Score}", prediction.Score);

                    // Ensure score is between 0 and 1
                    return Math.Max(0f, Math.Min(1f, prediction.Score));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during ML.NET prediction");
                    throw;
                }
            });
        }

        public async Task<string> TrainModelAsync()
        {
            try
            {
                _logger.LogInformation("Starting ML model training...");

                // Get training data from your existing service
                var trainingData = await _featureService.PrepareTrainingDataAsync();

                if (!trainingData.Any())
                {
                    _logger.LogWarning("No training data available, generating sample data");
                    trainingData = GenerateSampleTrainingData();
                }

                // Convert to ML.NET format
                var dataView = _mlContext.Data.LoadFromEnumerable(trainingData.Select(x => new SmartHiringTrainingInput
                {
                    SkillMatchScore = x.SkillMatchScore,
                    AvgRating = x.AvgRating,
                    RecommendationRate = x.RecommendationRate,
                    CompletionRate = x.CompletionRate,
                    BidSuccessRate = x.BidSuccessRate,
                    CategoryExperience = x.CategoryExperience,
                    ResponseTimeHours = x.ResponseTimeHours,
                    PortfolioQuality = x.PortfolioQuality,
                    BudgetMatchScore = x.BudgetMatchScore,
                    DeliveryTimeDays = x.DeliveryTimeDays,
                    FreelancerTenureDays = x.FreelancerTenureDays,
                    ProjectComplexity = x.ProjectComplexity,
                    ClientHistoryScore = x.ClientHistoryScore,
                    PastCollaboration = x.PastCollaboration,
                    SkillsCountMatch = x.SkillsCountMatch,
                    WorkloadFactor = x.WorkloadFactor,
                    MentorshipProgramCompleted = x.MentorshipProgramCompleted,
                    IsSuccessfulMatch = x.IsSuccessfulMatch == 1
                }));

                // Define training pipeline using FastTree (works like Random Forest)
                var pipeline = _mlContext.Transforms.Concatenate("Features",
                        nameof(SmartHiringTrainingInput.SkillMatchScore),
                        nameof(SmartHiringTrainingInput.AvgRating),
                        nameof(SmartHiringTrainingInput.RecommendationRate),
                        nameof(SmartHiringTrainingInput.CompletionRate),
                        nameof(SmartHiringTrainingInput.BidSuccessRate),
                        nameof(SmartHiringTrainingInput.CategoryExperience),
                        nameof(SmartHiringTrainingInput.ResponseTimeHours),
                        nameof(SmartHiringTrainingInput.PortfolioQuality),
                        nameof(SmartHiringTrainingInput.BudgetMatchScore),
                        nameof(SmartHiringTrainingInput.DeliveryTimeDays),
                        nameof(SmartHiringTrainingInput.FreelancerTenureDays),
                        nameof(SmartHiringTrainingInput.ProjectComplexity),
                        nameof(SmartHiringTrainingInput.ClientHistoryScore),
                        nameof(SmartHiringTrainingInput.PastCollaboration),
                        nameof(SmartHiringTrainingInput.SkillsCountMatch),
                        nameof(SmartHiringTrainingInput.WorkloadFactor),
                        nameof(SmartHiringTrainingInput.MentorshipProgramCompleted))
// Replace the FastTree line with this simpler trainer:
.Append(_mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(
    labelColumnName: nameof(SmartHiringTrainingInput.IsSuccessfulMatch),
    featureColumnName: "Features"));

                // Train the model
                _logger.LogInformation("Training ML model with {Count} samples", trainingData.Count);
                var model = pipeline.Fit(dataView);

                // Save the model
                var modelsDir = Path.Combine(_webHostEnvironment.WebRootPath, "models");
                Directory.CreateDirectory(modelsDir);

                var modelPath = Path.Combine(modelsDir, "smart_hiring_model.zip");
                _mlContext.Model.Save(model, dataView.Schema, modelPath);

                // Update the current instance
                _model = model;
                _predictionEngine = _mlContext.Model.CreatePredictionEngine<SmartHiringInput, SmartHiringOutput>(_model);
                _isInitialized = true;

                _logger.LogInformation("Model trained and saved to: {ModelPath}", modelPath);
                return modelPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to train ML model");
                throw;
            }
        }

        private float CalculateFallbackScore(Dictionary<string, object> features)
        {
            try
            {
                float score = 0f;

                // Weighted scoring based on feature importance
                score += Convert.ToSingle(features.GetValueOrDefault("skill_match_score", 0f)) * 0.23f;
                score += (Convert.ToSingle(features.GetValueOrDefault("avg_rating", 3.0f)) / 5.0f) * 0.18f;
                score += Convert.ToSingle(features.GetValueOrDefault("recommendation_rate", 0.5f)) * 0.14f;
                score += Convert.ToSingle(features.GetValueOrDefault("completion_rate", 0.8f)) * 0.14f;
                score += Convert.ToSingle(features.GetValueOrDefault("bid_success_rate", 0.1f)) * 0.09f;
                score += (Convert.ToSingle(features.GetValueOrDefault("category_experience", 0)) / 10.0f) * 0.09f;
                score += Convert.ToSingle(features.GetValueOrDefault("budget_match_score", 0.5f)) * 0.05f;
                score += Convert.ToSingle(features.GetValueOrDefault("mentorship_program_completed", 0)) * 0.08f;

                return Math.Max(0f, Math.Min(1f, score));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in fallback scoring");
                return 0.5f;
            }
        }

        private List<SmartHiringTrainingData> GenerateSampleTrainingData()
        {
            var random = new Random();
            var trainingData = new List<SmartHiringTrainingData>();

            for (int i = 0; i < 1000; i++)
            {
                var skillMatch = (float)random.NextDouble();
                var rating = (float)(3.0 + random.NextDouble() * 2.0);
                var completion = (float)(0.5 + random.NextDouble() * 0.5);

                // Generate target based on features
                var successProbability = (skillMatch * 0.4f + (rating / 5.0f) * 0.3f + completion * 0.3f);
                var isSuccessful = random.NextDouble() < successProbability ? 1 : 0;

                trainingData.Add(new SmartHiringTrainingData
                {
                    SkillMatchScore = skillMatch,
                    AvgRating = rating,
                    RecommendationRate = (float)random.NextDouble(),
                    CompletionRate = completion,
                    BidSuccessRate = (float)(random.NextDouble() * 0.3),
                    CategoryExperience = random.Next(0, 10),
                    ResponseTimeHours = (float)(random.NextDouble() * 48),
                    PortfolioQuality = (float)(3 + random.NextDouble() * 4),
                    BudgetMatchScore = (float)random.NextDouble(),
                    DeliveryTimeDays = (float)(1 + random.NextDouble() * 30),
                    FreelancerTenureDays = (float)(30 + random.NextDouble() * 1000),
                    ProjectComplexity = (float)(1 + random.NextDouble() * 9),
                    ClientHistoryScore = (float)random.NextDouble(),
                    PastCollaboration = random.Next(0, 2),
                    SkillsCountMatch = random.Next(0, 10),
                    WorkloadFactor = (float)random.NextDouble(),
                    MentorshipProgramCompleted = random.Next(0, 2),
                    IsSuccessfulMatch = isSuccessful
                });
            }

            return trainingData;
        }
    }

    // ML.NET model input classes
    public class SmartHiringInput
    {
        public float SkillMatchScore { get; set; }
        public float AvgRating { get; set; }
        public float RecommendationRate { get; set; }
        public float CompletionRate { get; set; }
        public float BidSuccessRate { get; set; }
        public float CategoryExperience { get; set; }
        public float ResponseTimeHours { get; set; }
        public float PortfolioQuality { get; set; }
        public float BudgetMatchScore { get; set; }
        public float DeliveryTimeDays { get; set; }
        public float FreelancerTenureDays { get; set; }
        public float ProjectComplexity { get; set; }
        public float ClientHistoryScore { get; set; }
        public float PastCollaboration { get; set; }
        public float SkillsCountMatch { get; set; }
        public float WorkloadFactor { get; set; }
        public float MentorshipProgramCompleted { get; set; }
    }

    public class SmartHiringTrainingInput : SmartHiringInput
    {
        public bool IsSuccessfulMatch { get; set; }
    }

    public class SmartHiringOutput
    {
        [ColumnName("PredictedLabel")]
        public bool PredictedLabel { get; set; }

        [ColumnName("Probability")]
        public float Probability { get; set; }

        [ColumnName("Score")]
        public float Score { get; set; }
    }
}