using System.Text.Json;

namespace Freelancing.Services
{
    public interface ILocalRandomForestService
    {
        Task<float> PredictAsync(Dictionary<string, object> features);
        bool IsAvailable { get; }
        Task EnsureInitializedAsync();
    }

    public class LocalRandomForestService : ILocalRandomForestService
    {
        private readonly ILogger<LocalRandomForestService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl;
        private bool _isInitialized = false;

        public LocalRandomForestService(ILogger<LocalRandomForestService> logger, HttpClient httpClient, IConfiguration configuration)
        {
            _logger = logger;
            _httpClient = httpClient;
            
            // Check for Railway environment variables first, then fallback to local
            _apiUrl = Environment.GetEnvironmentVariable("FLASK_API_URL") 
                     ?? configuration["FlaskAPI:Url"] 
                     ?? "http://flask-ml-api:5000";
                     
            _logger.LogInformation($"Flask API URL configured as: {_apiUrl}");
        }

        public bool IsAvailable => _isInitialized;

        public async Task EnsureInitializedAsync()
        {
            if (_isInitialized) return;

            // Remove Railway environment skip - we want to try initialization in all environments
            _logger.LogInformation($"Attempting to initialize Flask ML API at: {_apiUrl}");

            try
            {
                // Set timeout for health check
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                
                var response = await _httpClient.GetAsync($"{_apiUrl}/health", cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var health = JsonSerializer.Deserialize<HealthResponse>(content);
                    
                    if (health?.ModelLoaded == true)
                    {
                        _isInitialized = true;
                        _logger.LogInformation("Random Forest service initialized successfully");
                    }
                    else
                    {
                        _logger.LogWarning("Flask API available but model not loaded");
                    }
                }
                else
                {
                    _logger.LogWarning($"Flask API health check failed: {response.StatusCode}");
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Flask API health check timed out");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning($"Failed to connect to Flask API: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during Flask API initialization");
            }
        }

        public async Task<float> PredictAsync(Dictionary<string, object> features)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Random Forest service not available");
            }

            try
            {
                var requestData = new { features = features };
                var json = JsonSerializer.Serialize(requestData);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                // Set timeout for prediction request
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                
                var response = await _httpClient.PostAsync($"{_apiUrl}/predict", content, cts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<PredictionResponse>(responseContent);
                    
                    if (result?.Success == true)
                    {
                        _logger.LogInformation($"Random Forest prediction successful: {result.Prediction:F3}");
                        return result.Prediction;
                    }
                    else
                    {
                        throw new Exception($"Prediction failed: {result?.Message}");
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"HTTP request failed: {response.StatusCode} - {errorContent}");
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogError("Random Forest prediction timed out");
                throw new TimeoutException("Prediction request timed out");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Random Forest prediction failed");
                throw;
            }
        }

        private class HealthResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("status")]
            public string Status { get; set; } = "";
            
            [System.Text.Json.Serialization.JsonPropertyName("model_loaded")]
            public bool ModelLoaded { get; set; }
        }

        private class PredictionResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("success")]
            public bool Success { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("prediction")]
            public float Prediction { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("message")]
            public string Message { get; set; } = "";
        }
    }
}
