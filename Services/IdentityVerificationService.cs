using Freelancing.Data;
using Freelancing.Models;
using Freelancing.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Freelancing.Services
{
    public class IdentityVerificationService : IIdentityVerificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<IdentityVerificationService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IIdentityEncryptionService _encryptionService;
        private readonly string _googleCloudApiKey;
        private readonly string _uploadsPath;

        public IdentityVerificationService(
            ApplicationDbContext context,
            ILogger<IdentityVerificationService> logger,
            IConfiguration configuration,
            IIdentityEncryptionService encryptionService)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _encryptionService = encryptionService;
            _googleCloudApiKey = _configuration["GoogleCloud:VisionApiKey"];
            _uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "identity");

            if (string.IsNullOrEmpty(_googleCloudApiKey))
            {
                _logger.LogWarning("Google Cloud Vision API key not configured.");
            }

            // Ensure uploads directory exists
            if (!Directory.Exists(_uploadsPath))
            {
                Directory.CreateDirectory(_uploadsPath);
            }
        }

        public async Task<VerificationResultViewModel> VerifyIdentityAsync(IdentityVerificationViewModel model, string userId)
        {
            try
            {
                var result = new VerificationResultViewModel
                {
                    Success = true,
                    Message = "Verification completed successfully",
                    IdDocumentConfidence = 0.0f,
                    FaceConfidence = 0.0f
                };

                // Verify ID Document
                if (model.IdDocumentImage != null)
                {
                    var (idVerified, idMessage, idConfidence) = await VerifyIdDocumentAsync(model.IdDocumentImage, model.IdDocumentType, model.IdDocumentNumber, model.IdDocumentExpiryDate, model.IdDocumentHasNoExpiration, userId);
                    result.IdDocumentVerified = idVerified;
                    result.IdDocumentMessage = idMessage;
                    result.IdDocumentConfidence = idConfidence;
                }

                // Verify Live Face Capture
                if (!string.IsNullOrEmpty(model.LiveFaceImageData))
                {
                    var (faceVerified, faceMessage, faceConfidence) = await VerifyLiveFaceAsync(model.LiveFaceImageData, userId);
                    result.FaceVerified = faceVerified;
                    result.FaceMessage = faceMessage;
                    result.FaceConfidence = faceConfidence;
                }

                // Save verification data with encryption
                await SaveVerificationDataAsync(model, userId, result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during identity verification for user {UserId}", userId);
                return new VerificationResultViewModel
                {
                    Success = false,
                    Message = "An error occurred during verification. Please try again.",
                    IdDocumentVerified = false,
                    FaceVerified = false,
                    IdDocumentConfidence = 0.0f,
                    FaceConfidence = 0.0f
                };
            }
        }

        // New method for completing verification with stored document data
        public async Task<VerificationResultViewModel> CompleteVerificationAsync(string liveFaceImageData, string userId, string idDocumentType, string idDocumentNumber, DateTime? idDocumentExpiryDate, bool idDocumentHasNoExpiration, bool idDocumentVerified, float idDocumentConfidence)
        {
            try
            {
                var result = new VerificationResultViewModel
                {
                    Success = true,
                    Message = "Verification completed successfully",
                    IdDocumentVerified = idDocumentVerified,
                    IdDocumentConfidence = idDocumentConfidence,
                    IdDocumentMessage = "Document verified successfully",
                    FaceConfidence = 0.0f
                };

                // Verify Live Face Capture
                if (!string.IsNullOrEmpty(liveFaceImageData))
                {
                    var (faceVerified, faceMessage, faceConfidence) = await VerifyLiveFaceAsync(liveFaceImageData, userId);
                    result.FaceVerified = faceVerified;
                    result.FaceMessage = faceMessage;
                    result.FaceConfidence = faceConfidence;
                }

                // Create a model for saving
                var model = new IdentityVerificationViewModel
                {
                    IdDocumentType = idDocumentType,
                    IdDocumentNumber = idDocumentNumber,
                    IdDocumentExpiryDate = idDocumentExpiryDate,
                    IdDocumentHasNoExpiration = idDocumentHasNoExpiration,
                    LiveFaceImageData = liveFaceImageData
                };

                // Save verification data with encryption
                await SaveVerificationDataAsync(model, userId, result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during verification completion for user {UserId}", userId);
                return new VerificationResultViewModel
                {
                    Success = false,
                    Message = "An error occurred during verification completion. Please try again.",
                    IdDocumentVerified = idDocumentVerified,
                    FaceVerified = false,
                    IdDocumentConfidence = idDocumentConfidence,
                    FaceConfidence = 0.0f
                };
            }
        }

        public async Task<(bool verified, string message, float confidence)> VerifyIdDocumentAsync(IFormFile documentImage, string idDocumentType, string idDocumentNumber, DateTime? idDocumentExpiryDate, bool idDocumentHasNoExpiration, string userId)
        {
            try
            {
                // Save and encrypt the document image
                var imageBytes = await GetImageBytesAsync(documentImage);
                var encryptedImage = _encryptionService.EncryptDocumentImage(imageBytes, userId);
                
                // Process with Google Cloud Vision API using REST API
                if (!string.IsNullOrEmpty(_googleCloudApiKey))
                {
                    using var httpClient = new HttpClient();
                    var requestUrl = $"https://vision.googleapis.com/v1/images:annotate?key={_googleCloudApiKey}";
                    
                    var requestBody = new
                    {
                        requests = new[]
                        {
                            new
                            {
                                image = new
                                {
                                    content = Convert.ToBase64String(imageBytes)
                                },
                                features = new[]
                                {
                                    new
                                    {
                                        type = "TEXT_DETECTION",
                                        maxResults = 10
                                    }
                                }
                            }
                        }
                    };
                    
                    var jsonContent = System.Text.Json.JsonSerializer.Serialize(requestBody);
                    var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                    
                    var response = await httpClient.PostAsync(requestUrl, content);
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var visionResponse = System.Text.Json.JsonSerializer.Deserialize<dynamic>(responseContent);
                        var textAnnotations = visionResponse.GetProperty("responses")[0].GetProperty("textAnnotations");
                        
                        var extractedText = "";
                        if (textAnnotations.GetArrayLength() > 0)
                        {
                            extractedText = textAnnotations[0].GetProperty("description").GetString();
                        }
                        
                        // Basic validation - check if it looks like an ID document
                        var hasNumbers = extractedText.Any(char.IsDigit);
                        var hasLetters = extractedText.Any(char.IsLetter);
                        var hasDatePattern = System.Text.RegularExpressions.Regex.IsMatch(extractedText, @"\d{1,2}[/-]\d{1,2}[/-]\d{2,4}");
                        
                        // ID documents need at least numbers and letters, but date pattern is optional
                        if (hasNumbers && hasLetters)
                        {
                            if (hasDatePattern)
                            {
                                return (true, "ID document appears valid with expiration date", 90.0f);
                            }
                            else
                            {
                                return (true, "ID document appears valid (no expiration date detected)", 85.0f);
                            }
                        }
                        
                        return (false, "Unable to verify ID document. Please ensure the image is clear and contains readable text.", 0.0f);
                    }
                    else
                    {
                        _logger.LogError("Google Vision API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                        return (false, "Error processing ID document with Google Vision API.", 0.0f);
                    }
                }
                else
                {
                    return (false, "Google Cloud Vision API key not configured.", 0.0f);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying ID document for user {UserId}", userId);
                return (false, "Error processing ID document. Please try again.", 0.0f);
            }
        }

        public async Task<(bool verified, string message, float confidence)> VerifyLiveFaceAsync(string base64ImageData, string userId)
        {
            try
            {
                // Convert base64 to bytes
                var imageBytes = Convert.FromBase64String(base64ImageData.Replace("data:image/jpeg;base64,", ""));
                
                // Encrypt the face image
                var encryptedImage = _encryptionService.EncryptDocumentImage(imageBytes, userId);

                // Process with Google Cloud Vision API using REST API
                if (!string.IsNullOrEmpty(_googleCloudApiKey))
                {
                    using var httpClient = new HttpClient();
                    var requestUrl = $"https://vision.googleapis.com/v1/images:annotate?key={_googleCloudApiKey}";
                    
                    var requestBody = new
                    {
                        requests = new[]
                        {
                            new
                            {
                                image = new
                                {
                                    content = Convert.ToBase64String(imageBytes)
                                },
                                features = new[]
                                {
                                    new
                                    {
                                        type = "FACE_DETECTION",
                                        maxResults = 10
                                    }
                                }
                            }
                        }
                    };
                    
                    var jsonContent = System.Text.Json.JsonSerializer.Serialize(requestBody);
                    var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                    
                    var response = await httpClient.PostAsync(requestUrl, content);
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var visionResponse = System.Text.Json.JsonSerializer.Deserialize<dynamic>(responseContent);
                        var faceAnnotations = visionResponse.GetProperty("responses")[0].GetProperty("faceAnnotations");
                        
                        if (faceAnnotations.GetArrayLength() == 0)
                        {
                            return (false, "No face detected. Please ensure your face is clearly visible in the camera.", 0.0f);
                        }

                        if (faceAnnotations.GetArrayLength() > 1)
                        {
                            return (false, "Multiple faces detected. Please ensure only your face is visible.", 0.0f);
                        }

                        var faceDetails = faceAnnotations[0];
                        var confidence = faceDetails.GetProperty("detectionConfidence").GetSingle() * 100; // Convert to percentage

                        // Basic face quality checks
                        if (confidence < 50.0f)
                        {
                            return (false, "Face quality is too low. Please ensure good lighting and clear visibility.", 0.0f);
                        }

                        // Check if face is too blurry
                        if (confidence < 70.0f)
                        {
                            return (false, "Image is too blurry. Please take a clearer photo.", 0.0f);
                        }

                        return (true, "Face verification successful", confidence);
                    }
                    else
                    {
                        _logger.LogError("Google Vision API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                        return (false, "Error processing face verification with Google Vision API.", 0.0f);
                    }
                }
                else
                {
                    return (false, "Google Cloud Vision API key not configured.", 0.0f);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying live face for user {UserId}", userId);
                return (false, "Error processing face verification. Please try again.", 0.0f);
            }
        }

        private async Task SaveVerificationDataAsync(IdentityVerificationViewModel model, string userId, VerificationResultViewModel result)
        {
            // Create or update verification record
            var verification = await _context.IdentityVerifications
                .FirstOrDefaultAsync(v => v.UserAccountId == userId);

            if (verification == null)
            {
                verification = new IdentityVerification
                {
                    Id = Guid.NewGuid(),
                    UserAccountId = userId,
                    Status = "PENDING",
                    CreatedAt = DateTime.UtcNow.ToLocalTime(),
                    UpdatedAt = DateTime.UtcNow.ToLocalTime(),
                    CreatedBy = userId,
                    UpdatedBy = userId
                };
                _context.IdentityVerifications.Add(verification);
            }

            // Update verification data with encryption
            if (model.IdDocumentImage != null)
            {
                var imageBytes = await GetImageBytesAsync(model.IdDocumentImage);
                verification.EncryptedIdDocumentImage = _encryptionService.EncryptDocumentImage(imageBytes, userId);
                verification.IdDocumentType = model.IdDocumentType;
                verification.EncryptedIdDocumentNumber = _encryptionService.EncryptIdentityData(model.IdDocumentNumber, userId);
                // If user checked "no expiration", set a far future date; otherwise use the selected date
                verification.IdDocumentExpiryDate = model.IdDocumentHasNoExpiration 
                    ? DateTime.UtcNow.ToLocalTime().AddYears(100) // Set to 100 years in future for "no expiration"
                    : model.IdDocumentExpiryDate;
                verification.IdDocumentVerified = result.IdDocumentVerified;
                verification.IdDocumentConfidence = result.IdDocumentConfidence;
            }
            else if (!string.IsNullOrEmpty(model.IdDocumentType))
            {
                // Document data was already processed in a previous step
                verification.IdDocumentType = model.IdDocumentType;
                verification.EncryptedIdDocumentNumber = _encryptionService.EncryptIdentityData(model.IdDocumentNumber, userId);
                verification.IdDocumentExpiryDate = model.IdDocumentHasNoExpiration 
                    ? DateTime.UtcNow.ToLocalTime().AddYears(100) // Set to 100 years in future for "no expiration"
                    : model.IdDocumentExpiryDate;
                verification.IdDocumentVerified = result.IdDocumentVerified;
                verification.IdDocumentConfidence = result.IdDocumentConfidence;
            }

            if (!string.IsNullOrEmpty(model.LiveFaceImageData))
            {
                var imageBytes = Convert.FromBase64String(model.LiveFaceImageData.Replace("data:image/jpeg;base64,", ""));
                verification.EncryptedFaceImage = _encryptionService.EncryptDocumentImage(imageBytes, userId);
                verification.FaceVerified = result.FaceVerified;
                verification.FaceConfidence = result.FaceConfidence;
            }

            // Determine overall status
            if (result.IdDocumentVerified == true && result.FaceVerified == true)
            {
                verification.Status = "APPROVED";
                verification.VerifiedAt = DateTime.UtcNow.ToLocalTime();
            }
            else if (result.IdDocumentVerified == false || result.FaceVerified == false)
            {
                verification.Status = "REJECTED";
                verification.RejectedAt = DateTime.UtcNow.ToLocalTime();
                verification.RejectionReason = $"ID: {result.IdDocumentMessage}, Face: {result.FaceMessage}";
            }

            verification.UpdatedAt = DateTime.UtcNow.ToLocalTime();
            verification.UpdatedBy = userId;

            await _context.SaveChangesAsync();
        }

        private async Task<byte[]> GetImageBytesAsync(IFormFile file)
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }

        public async Task<VerificationStatusViewModel?> GetVerificationStatusAsync(string userId)
        {
            var verification = await _context.IdentityVerifications
                .FirstOrDefaultAsync(v => v.UserAccountId == userId);

            if (verification == null)
                return null;
            
            return new VerificationStatusViewModel
            {
                Id = verification.Id,
                Status = verification.Status,
                IdDocumentType = verification.IdDocumentType,
                IdDocumentVerified = verification.IdDocumentVerified,
                IdDocumentConfidence = verification.IdDocumentConfidence,
                FaceVerified = verification.FaceVerified,
                FaceConfidence = verification.FaceConfidence,
                CreatedAt = verification.CreatedAt,
                VerifiedAt = verification.VerifiedAt,
                RejectedAt = verification.RejectedAt,
                RejectionReason = verification.RejectionReason,
                IsEncrypted = verification.IsEncrypted,
                EncryptionMethod = verification.EncryptionMethod
            };
        }

        public async Task<bool> IsUserVerifiedAsync(string userId)
        {
            var verification = await _context.IdentityVerifications
                .FirstOrDefaultAsync(v => v.UserAccountId == userId);

            return verification?.Status == "APPROVED";
        }

        public async Task<bool> CanUserPostProjectAsync(string userId)
        {
            return await IsUserVerifiedAsync(userId);
        }

        public async Task<bool> CanUserBidAsync(string userId)
        {
            return await IsUserVerifiedAsync(userId);
        }

        public async Task<IdentityVerification?> GetLatestVerificationAsync(string userId)
        {
            return await _context.IdentityVerifications
                .FirstOrDefaultAsync(v => v.UserAccountId == userId);
        }

        public async Task<bool> UpdateVerificationStatusAsync(Guid verificationId, string status, string? reason = null)
        {
            var verification = await _context.IdentityVerifications
                .FirstOrDefaultAsync(v => v.Id == verificationId);

            if (verification == null)
                return false;

            verification.Status = status;
            verification.UpdatedAt = DateTime.UtcNow.ToLocalTime();

            if (status == "APPROVED")
            {
                verification.VerifiedAt = DateTime.UtcNow.ToLocalTime();
            }
            else if (status == "REJECTED")
            {
                verification.RejectedAt = DateTime.UtcNow.ToLocalTime();
                verification.RejectionReason = reason;
            }

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
