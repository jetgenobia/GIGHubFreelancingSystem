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
        private bool _isInitialized = false;
        private readonly string _apiUrl = "http://flask-ml-api:5000";

        public LocalRandomForestService(ILogger<LocalRandomForestService> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
        }

        public bool IsAvailable => _isInitialized;

        public async Task EnsureInitializedAsync()
        {
            if (_isInitialized) return;

            try
            {
                var response = await _httpClient.GetAsync($"{_apiUrl}/health");
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
                    _logger.LogWarning("Flask API health check failed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Flask API");
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
                
                var response = await _httpClient.PostAsync($"{_apiUrl}/predict", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<PredictionResponse>(responseContent);
                    
                    if (result?.Success == true)
                    {
                        return result.Prediction;
                    }
                    else
                    {
                        throw new Exception($"Prediction failed: {result?.Message}");
                    }
                }
                else
                {
                    throw new Exception($"HTTP request failed: {response.StatusCode}");
                }
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
