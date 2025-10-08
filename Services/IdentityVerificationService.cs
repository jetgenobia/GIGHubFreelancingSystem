using Freelancing.Data;
using Freelancing.Models;
using Freelancing.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;

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

                DateTime? extractedExpiryDate = null; // Variable to hold extracted expiry date

                // Verify ID Document
                if (model.IdDocumentImage != null)
                {
                    var (idVerified, idMessage, idConfidence, extractedName, extractedIdNumber, extractedExpiry) = await VerifyIdDocumentAsync(
                        model.IdDocumentImage,
                        model.IdDocumentType,
                        null, // No longer using manual input
                        model.IdDocumentExpiryDate,
                        model.IdDocumentHasNoExpiration,
                        userId
                    );
                    result.IdDocumentVerified = idVerified;
                    result.IdDocumentMessage = idMessage;
                    result.IdDocumentConfidence = idConfidence;
                    result.ExtractedIdName = extractedName;
                    result.ExtractedIdNumber = extractedIdNumber;
                    extractedExpiryDate = extractedExpiry; // Store the extracted expiry date
                }

                // Verify Live Face Capture
                if (!string.IsNullOrEmpty(model.LiveFaceImageData))
                {
                    var (faceVerified, faceMessage, faceConfidence) = await VerifyLiveFaceAsync(model.LiveFaceImageData, userId);
                    result.FaceVerified = faceVerified;
                    result.FaceMessage = faceMessage;
                    result.FaceConfidence = faceConfidence;
                }

                // Save verification data with encryption, passing the extracted expiry date
                await SaveVerificationDataAsync(model, userId, result, extractedExpiryDate);

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
        public async Task<VerificationResultViewModel> CompleteVerificationAsync(
            string liveFaceImageData,
            string userId,
            string idDocumentType,
            string? extractedIdNumber,
            DateTime? idDocumentExpiryDate,
            bool idDocumentHasNoExpiration,
            bool idDocumentVerified,
            float idDocumentConfidence,
            string? extractedIdName,
            string? storedIdDocumentImageBase64 = null)
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
                    ExtractedIdNumber = extractedIdNumber,
                    ExtractedIdName = extractedIdName,
                    FaceConfidence = 0.0f
                };

                if (idDocumentVerified && !string.IsNullOrEmpty(extractedIdName))
                {
                    var (nameMatch, nameMatchMessage) = await CrossMatchNameWithRegisteredUserAsync(extractedIdName, userId);
                    if (!nameMatch)
                    {
                        result.IdDocumentVerified = false;
                        result.IdDocumentMessage = nameMatchMessage;
                        result.IdDocumentConfidence = 0.0f;
                    }
                }

                // Verify Live Face Capture
                if (!string.IsNullOrEmpty(liveFaceImageData))
                {
                    var (faceVerified, faceMessage, faceConfidence) = await VerifyLiveFaceAsync(liveFaceImageData, userId);
                    result.FaceVerified = faceVerified;
                    result.FaceMessage = faceMessage;
                    result.FaceConfidence = faceConfidence;
                }

                // Build model for saving; include stored base64 image if present
                var model = new IdentityVerificationViewModel
                {
                    IdDocumentType = idDocumentType,
                    ExtractedIdNumber = extractedIdNumber,
                    ExtractedIdName = extractedIdName,
                    IdDocumentExpiryDate = idDocumentExpiryDate,
                    IdDocumentHasNoExpiration = idDocumentHasNoExpiration,
                    LiveFaceImageData = liveFaceImageData,
                    StoredIdDocumentImageData = storedIdDocumentImageBase64
                };

                // NEW: Pass the idDocumentExpiryDate as extractedExpiryDate since this comes from the controller
                // which already processed the extracted expiry date
                await SaveVerificationDataAsync(model, userId, result, idDocumentExpiryDate);

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

        public async Task<(bool verified, string message, float confidence, string? extractedIdName, string? extractedIdNumber, DateTime? extractedExpiryDate)> VerifyIdDocumentAsync(
    IFormFile? documentImage,
    string idDocumentType,
    string? manualIdNumber,
    DateTime? idDocumentExpiryDate,
    bool idDocumentHasNoExpiration,
    string userId,
    byte[]? storedImageBytes = null)
        {
            try
            {
                byte[] imageBytes;

                // Use stored image bytes if provided, otherwise use the uploaded file
                if (storedImageBytes != null)
                {
                    imageBytes = storedImageBytes;
                }
                else if (documentImage != null)
                {
                    imageBytes = await GetImageBytesAsync(documentImage);
                }
                else
                {
                    return (false, "No document image provided for verification.", 0.0f, null, null, null);
                }

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
                        try
                        {
                            var visionResponse = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(responseContent);

                            // FIXED: Check if responses array exists and has elements
                            if (!visionResponse.TryGetProperty("responses", out var responsesElement) ||
                                responsesElement.GetArrayLength() == 0)
                            {
                                _logger.LogWarning("Google Vision API returned empty responses for user {UserId}", userId);
                                return (false, "No text could be detected in the document image. Please ensure the image is clear and contains readable text.", 0.0f, null, null, null);
                            }

                            var firstResponse = responsesElement[0];

                            // FIXED: Check if textAnnotations exists before trying to access it
                            if (!firstResponse.TryGetProperty("textAnnotations", out var textAnnotations))
                            {
                                _logger.LogWarning("No text annotations found in Google Vision response for user {UserId}", userId);
                                return (false, "No readable text detected in the document. Please ensure the image is clear and well-lit.", 0.0f, null, null, null);
                            }

                            var extractedText = "";
                            if (textAnnotations.GetArrayLength() > 0)
                            {
                                if (textAnnotations[0].TryGetProperty("description", out var descriptionElement))
                                {
                                    extractedText = descriptionElement.GetString() ?? "";
                                }
                            }

                            if (string.IsNullOrEmpty(extractedText))
                            {
                                _logger.LogWarning("Empty text extracted from document for user {UserId}", userId);
                                return (false, "No readable text could be extracted from the document. Please ensure the image is clear and contains visible text.", 0.0f, null, null, null);
                            }

                            // Parse the extracted text based on document type
                            IdOcrResult ocrResult = null;

                            // ENHANCED: Try all parsing methods to get the best result
                            var allResults = new List<IdOcrResult>();

                            // Parse with all methods to find any ID patterns
                            allResults.Add(ParseNationalId(extractedText));
                            allResults.Add(ParseDriversLicense(extractedText));
                            allResults.Add(ParsePassport(extractedText));
                            allResults.Add(ParseGenericId(extractedText));

                            // Find the result with the most complete information
                            ocrResult = allResults
                                .Where(r => r != null && !string.IsNullOrEmpty(r.IdNumber))
                                .OrderByDescending(r => (!string.IsNullOrEmpty(r.Name) ? 1 : 0) + (!string.IsNullOrEmpty(r.IdNumber) ? 1 : 0))
                                .FirstOrDefault();

                            // If we couldn't parse with specific methods, try generic extraction
                            if (ocrResult == null || string.IsNullOrEmpty(ocrResult.IdNumber))
                            {
                                ocrResult = ExtractAnyIdPattern(extractedText);
                            }

                            // Basic validation - check if it looks like an ID document
                            var hasNumbers = extractedText.Any(char.IsDigit);
                            var hasLetters = extractedText.Any(char.IsLetter);

                            // Log the OCR results for debugging
                            _logger.LogInformation("OCR Results for {DocumentType}: Name='{ExtractedName}', ID='{ExtractedId}', ExpiryDate='{ExtractedExpiry}', Raw text length: {TextLength}",
                                idDocumentType, ocrResult?.Name, ocrResult?.IdNumber, ocrResult?.ExpiryDate, extractedText.Length);

                            // CRITICAL FIX: Check for document type mismatch BEFORE returning success
                            if (hasNumbers && hasLetters && ocrResult != null && !string.IsNullOrEmpty(ocrResult.IdNumber))
                            {
                                // FIRST: Check if the extracted ID matches the selected document type
                                var detectedDocumentType = DetectDocumentTypeFromIdNumber(ocrResult.IdNumber);
                                if (!string.IsNullOrEmpty(detectedDocumentType) &&
                                    !string.Equals(detectedDocumentType, idDocumentType, StringComparison.OrdinalIgnoreCase))
                                {
                                    _logger.LogWarning("Document type mismatch for user {UserId}. Selected: '{SelectedType}', Detected: '{DetectedType}', ID: '{ExtractedId}'",
                                        userId, idDocumentType, detectedDocumentType, ocrResult.IdNumber);

                                    // Return the extracted data but mark as NOT verified due to type mismatch
                                    return (false, "Document type mismatch detected", 0.0f, ocrResult.Name, ocrResult.IdNumber, ocrResult.ExpiryDate);
                                }

                                var confidence = 85.0f;
                                var message = "ID document processed successfully";

                                // Give higher confidence if we extracted both name and ID number
                                if (!string.IsNullOrEmpty(ocrResult.Name) && !string.IsNullOrEmpty(ocrResult.IdNumber))
                                {
                                    confidence = 95.0f;
                                    message = "ID document verified with extracted information";
                                }
                                else if (!string.IsNullOrEmpty(ocrResult.IdNumber))
                                {
                                    confidence = 90.0f;
                                    message = "ID document verified with extracted ID number";
                                }

                                // Cross-match extracted name with registered user name
                                if (!string.IsNullOrEmpty(ocrResult.Name))
                                {
                                    var (nameMatch, nameMatchMessage) = await CrossMatchNameWithRegisteredUserAsync(ocrResult.Name, userId);
                                    if (!nameMatch)
                                    {
                                        _logger.LogWarning("Name mismatch for user {UserId}. Extracted: '{ExtractedName}'", userId, ocrResult.Name);
                                        return (false, nameMatchMessage, 0.0f, ocrResult.Name, ocrResult.IdNumber, ocrResult.ExpiryDate);
                                    }
                                    else
                                    {
                                        _logger.LogInformation("Name match successful for user {UserId}", userId);
                                        message = "ID document verified with name match";
                                    }
                                }

                                // Return success only if document type matches
                                return (true, message, confidence, ocrResult.Name, ocrResult.IdNumber, ocrResult.ExpiryDate);
                            }

                            // Return partial results if we found something
                            if (ocrResult != null && (!string.IsNullOrEmpty(ocrResult.IdNumber) || !string.IsNullOrEmpty(ocrResult.Name)))
                            {
                                return (false, "Partial information extracted from document.", 0.0f, ocrResult.Name, ocrResult.IdNumber, ocrResult.ExpiryDate);
                            }

                            return (false, "Unable to extract required information from ID document. Please ensure the image is clear and contains readable text.", 0.0f, null, null, null);
                        }
                        catch (JsonException jsonEx)
                        {
                            _logger.LogError(jsonEx, "Error parsing Google Vision API response for user {UserId}. Response: {Response}", userId, responseContent);
                            return (false, "Error processing document image response. Please try again.", 0.0f, null, null, null);
                        }
                        catch (KeyNotFoundException keyEx)
                        {
                            _logger.LogError(keyEx, "Missing expected key in Google Vision API response for user {UserId}. Response: {Response}", userId, responseContent);
                            return (false, "Unexpected response format from document processing service. Please try again.", 0.0f, null, null, null);
                        }
                    }
                    else
                    {
                        _logger.LogError("Google Vision API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                        return (false, "Error processing ID document with Google Vision API.", 0.0f, null, null, null);
                    }
                }
                else
                {
                    return (false, "Google Cloud Vision API key not configured.", 0.0f, null, null, null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying ID document for user {UserId}", userId);
                return (false, "Error processing ID document. Please try again.", 0.0f, null, null, null);
            }
        }

        // Add this helper method to detect document type from ID number patterns
        private string DetectDocumentTypeFromIdNumber(string idNumber)
        {
            if (string.IsNullOrEmpty(idNumber)) return null;

            // Clean the ID number for better matching
            var cleanedId = idNumber.Trim().ToUpperInvariant();

            // Philippine National ID patterns - most restrictive first
            if (System.Text.RegularExpressions.Regex.IsMatch(cleanedId, @"^\d{4}[-\s]?\d{4}[-\s]?\d{4}[-\s]?\d{4}$"))
            {
                return "National ID"; // ####-####-####-#### or #### #### #### ####
            }

            // Driver's License patterns
            if (System.Text.RegularExpressions.Regex.IsMatch(cleanedId, @"^[A-Z]\d{2}[-\s]?\d{2}[-\s]?\d{5}$"))
            {
                return "Driver's License"; // N##-##-##### or N## ## #####
            }

            // Passport patterns
            if (System.Text.RegularExpressions.Regex.IsMatch(cleanedId, @"^P\d{7}[A-Z]?$"))
            {
                return "Passport"; // P#######A or P#######
            }

            // Additional flexible patterns for partial matches
            // Check for partial National ID (might have OCR errors)
            if (System.Text.RegularExpressions.Regex.IsMatch(cleanedId, @"^\d{4}.*\d{4}.*\d{4}.*\d{4}$"))
            {
                return "National ID"; // Flexible National ID pattern
            }

            // Check for partial Driver's License
            if (System.Text.RegularExpressions.Regex.IsMatch(cleanedId, @"^[A-Z]\d{2}.*\d{2}.*\d{5}$"))
            {
                return "Driver's License"; // Flexible Driver's License pattern
            }

            // Check for partial Passport
            if (cleanedId.StartsWith("P") && System.Text.RegularExpressions.Regex.IsMatch(cleanedId, @"^P\d{7,8}[A-Z]?$"))
            {
                return "Passport"; // Flexible Passport pattern
            }

            return null; // Unknown pattern
        }

        // Add this new method to extract any ID pattern from text
        private IdOcrResult ExtractAnyIdPattern(string text)
        {
            var result = new IdOcrResult();

            // Try to find any ID-like patterns
            var idPatterns = new[]
            {
        @"\b\d{4}[-\s]?\d{4}[-\s]?\d{4}[-\s]?\d{4}\b", // National ID pattern
        @"\b[A-Z]\d{2}[-\s]?\d{2}[-\s]?\d{5}\b",        // Driver's License pattern  
        @"\bP\d{7}[A-Z]?\b",                           // Passport pattern
        @"\b\d{8,12}\b",                               // Generic 8-12 digit number
        @"\b[A-Z0-9]{8,12}\b"                          // Generic alphanumeric
    };

            foreach (var pattern in idPatterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(text, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    result.IdNumber = match.Value.Trim();
                    break;
                }
            }

            // Try to extract any name-like patterns
            var namePatterns = new[]
            {
        @"([A-Z][a-z]+\s*,\s*[A-Z][a-z\s]+)", // "Surname, Given names"
        @"([A-Z]{2,}(?:\s+[A-Z]{2,})*,\s*[A-Z]{2,}(?:\s+[A-Z]{2,})*)", // "SURNAME, GIVEN NAMES"
        @"([A-Z]{2,}(?:\s+[A-Z]{2,}){1,3})" // Consecutive capitalized words
    };

            foreach (var pattern in namePatterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(text, pattern);
                if (match.Success && IsValidName(match.Groups[1].Value))
                {
                    result.Name = match.Groups[1].Value.Trim();
                    break;
                }
            }

            return result;
        }

        private bool IsValidName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            // Basic validation for names
            var invalidWords = new[]
            {
        "REPUBLIC", "PHILIPPINES", "DEPARTMENT", "GOVERNMENT", "AUTHORITY",
        "LICENSE", "PASSPORT", "NATIONAL", "STATISTICS", "PSA"
    };

            var upperName = name.ToUpperInvariant();
            return !invalidWords.Any(word => upperName.Contains(word)) &&
                   name.Length >= 5 && name.Length <= 50;
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

        private async Task SaveVerificationDataAsync(IdentityVerificationViewModel model,
            string userId,
            VerificationResultViewModel result,
            DateTime? extractedExpiryDate = null)
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
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = userId,
                    UpdatedBy = userId
                };
                _context.IdentityVerifications.Add(verification);
            }

            // If a new uploaded IFormFile exists, use it first
            if (model.IdDocumentImage != null)
            {
                var imageBytes = await GetImageBytesAsync(model.IdDocumentImage);
                verification.EncryptedIdDocumentImage = _encryptionService.EncryptDocumentImage(imageBytes, userId);
                verification.IdDocumentType = model.IdDocumentType;
                verification.EncryptedIdDocumentNumber = !string.IsNullOrEmpty(result.ExtractedIdNumber)
                    ? _encryptionService.EncryptIdentityData(result.ExtractedIdNumber, userId)
                    : null;

                // UPDATED: Use extracted expiry date with priority over manual input
                verification.IdDocumentExpiryDate = GetEffectiveExpiryDate(model, extractedExpiryDate);
                verification.IdDocumentVerified = result.IdDocumentVerified;
                verification.IdDocumentConfidence = result.IdDocumentConfidence;
            }
            else if (!string.IsNullOrEmpty(model.StoredIdDocumentImageData))
            {
                // Handle base64 image saved in session
                try
                {
                    var base64 = model.StoredIdDocumentImageData;
                    // Remove data url prefix if present
                    var idx = base64.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                    {
                        base64 = base64.Substring(idx + 7);
                    }

                    var imageBytes = Convert.FromBase64String(base64);
                    verification.EncryptedIdDocumentImage = _encryptionService.EncryptDocumentImage(imageBytes, userId);
                    verification.IdDocumentType = model.IdDocumentType;
                    verification.EncryptedIdDocumentNumber = !string.IsNullOrEmpty(result.ExtractedIdNumber)
                        ? _encryptionService.EncryptIdentityData(result.ExtractedIdNumber, userId)
                        : null;

                    // UPDATED: Use extracted expiry date with priority over manual input
                    verification.IdDocumentExpiryDate = GetEffectiveExpiryDate(model, extractedExpiryDate);
                    verification.IdDocumentVerified = result.IdDocumentVerified;
                    verification.IdDocumentConfidence = result.IdDocumentConfidence;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save stored Base64 document image for user {UserId}", userId);
                    // do not throw — allow record to continue without image
                }
            }
            else if (!string.IsNullOrEmpty(model.IdDocumentType))
            {
                // Document data was already processed in a previous step (no image to update)
                verification.IdDocumentType = model.IdDocumentType;
                verification.EncryptedIdDocumentNumber = !string.IsNullOrEmpty(result.ExtractedIdNumber)
                    ? _encryptionService.EncryptIdentityData(result.ExtractedIdNumber, userId)
                    : null;

                // UPDATED: Use extracted expiry date with priority over manual input
                verification.IdDocumentExpiryDate = GetEffectiveExpiryDate(model, extractedExpiryDate);
                verification.IdDocumentVerified = result.IdDocumentVerified;
                verification.IdDocumentConfidence = result.IdDocumentConfidence;
            }

            if (!string.IsNullOrEmpty(model.LiveFaceImageData))
            {
                try
                {
                    var faceBytes = Convert.FromBase64String(model.LiveFaceImageData.Replace("data:image/jpeg;base64,", ""));
                    verification.EncryptedFaceImage = _encryptionService.EncryptDocumentImage(faceBytes, userId);
                    verification.FaceVerified = result.FaceVerified;
                    verification.FaceConfidence = result.FaceConfidence;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to encrypt face image for user {UserId}", userId);
                }
            }

            // Preserve APPROVED status if already approved; otherwise set PENDING
            if (!string.Equals(verification.Status, "APPROVED", StringComparison.OrdinalIgnoreCase))
            {
                verification.Status = "PENDING";
                verification.VerifiedAt = null;
                verification.RejectedAt = null;
                verification.RejectionReason = null;
            }

            verification.UpdatedAt = DateTime.UtcNow;
            verification.UpdatedBy = userId;

            await _context.SaveChangesAsync();
        }

        private DateTime? GetEffectiveExpiryDate(IdentityVerificationViewModel model, DateTime? extractedExpiryDate)
        {
            // For National ID, always return null (they don't expire)
            if (string.Equals(model.IdDocumentType, "National ID", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // If marked as no expiration, return far future date (ensure UTC kind)
            if (model.IdDocumentHasNoExpiration)
            {
                return DateTime.UtcNow.AddYears(100);
            }

            // Priority: extracted date from OCR > manually entered date
            var candidate = extractedExpiryDate ?? model.IdDocumentExpiryDate;

            if (!candidate.HasValue)
                return null;

            // PostgreSQL + Npgsql expects DateTime.Kind==Utc for timestamptz.
            // Convert or specify UTC kind to avoid "Cannot write DateTime with Kind=Unspecified" errors.
            var dt = candidate.Value;

            if (dt.Kind == DateTimeKind.Utc)
                return dt;

            if (dt.Kind == DateTimeKind.Local)
                return dt.ToUniversalTime();

            // dt.Kind == Unspecified: preserve the same UTC moment by specifying kind as UTC.
            // This avoids Npgsql rejecting the value. We treat unspecified as a date/time value and mark it UTC.
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
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
            verification.UpdatedAt = DateTime.UtcNow;

            if (status == "APPROVED")
            {
                verification.VerifiedAt = DateTime.UtcNow;
            }
            else if (status == "REJECTED")
            {
                verification.RejectedAt = DateTime.UtcNow;
                verification.RejectionReason = reason;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        // Helper class for parsed OCR results
        private class IdOcrResult
        {
            public string IdNumber { get; set; }
            public string Name { get; set; }
            public DateTime? ExpiryDate { get; set; }
        }

        private IdOcrResult ParseNationalId(string ocrText)
        {
            var result = new IdOcrResult();

            // ID Number: ####-####-####-#### (unchanged)
            var idNumberMatch = Regex.Match(ocrText, @"\b\d{4}-\d{4}-\d{4}-\d{4}\b");
            result.IdNumber = idNumberMatch.Success ? idNumberMatch.Value : null;

            // Split lines for robust extraction
            var lines = ocrText.Split('\n').Select(l => l.Trim()).ToList();

            string lastName = "", givenNames = "", middleName = "";

            for (int i = 0; i < lines.Count; i++)
            {
                // Last Name
                if (Regex.IsMatch(lines[i], @"Apelyido/Last Name|Apleyido/Last Name", RegexOptions.IgnoreCase))
                {
                    if (i + 1 < lines.Count && !string.IsNullOrWhiteSpace(lines[i + 1]))
                        lastName = lines[i + 1].Trim();
                }
                // Given Names
                if (Regex.IsMatch(lines[i], @"Mga Pangalan/Given Names", RegexOptions.IgnoreCase))
                {
                    if (i + 1 < lines.Count && !string.IsNullOrWhiteSpace(lines[i + 1]))
                        givenNames = lines[i + 1].Trim();
                }
                // Middle Name
                if (Regex.IsMatch(lines[i], @"Gitnang Apelyido / Middle Name|Gitnang Apelyido/Middle Name", RegexOptions.IgnoreCase))
                {
                    if (i + 1 < lines.Count && !string.IsNullOrWhiteSpace(lines[i + 1]))
                        middleName = lines[i + 1].Trim();
                }
            }

            // Clean up extracted names
            lastName = CleanName(lastName);
            givenNames = CleanName(givenNames);
            middleName = CleanName(middleName);

            // Construct full name in Philippine format (Given Middle Last)
            var nameParts = new List<string>();
            if (!string.IsNullOrEmpty(givenNames)) nameParts.Add(givenNames);
            if (!string.IsNullOrEmpty(middleName)) nameParts.Add(middleName);
            if (!string.IsNullOrEmpty(lastName)) nameParts.Add(lastName);

            result.Name = string.Join(" ", nameParts).Trim();

            // National IDs don't have expiry dates
            result.ExpiryDate = null;

            _logger.LogInformation("Final parsed name: {FullName}", result.Name);

            return result;
        }

        private IdOcrResult ParseDriversLicense(string ocrText)
        {
            var result = new IdOcrResult();

            // License No.: N##-##-#####
            var idNumberMatch = Regex.Match(ocrText, @"License No\.?\s*([A-Z0-9\-]+)");
            result.IdNumber = idNumberMatch.Success ? idNumberMatch.Groups[1].Value.Trim() : null;

            // Improved name extraction for Philippine driver's license
            // Look for the pattern after "Last Name, First Name, Middle Name" header
            var namePatterns = new[]
            {
        // Pattern 1: Look for name after the header, before "Nationality"
        @"Last\s+Name,?\s*First\s+Name,?\s*Middle\s+Name\s*\n\s*([A-Z\s,]+?)(?:\s*\n\s*Nationality|\s*Nationality)",
        
        // Pattern 2: Direct comma-separated name pattern with length limits
        @"^([A-Z]+,\s*[A-Z]+(?:\s+[A-Z]+)?)(?:\s*\n|\s*$)",
        
        // Pattern 3: Look for capitalized words in comma format, but limit to reasonable length
        @"([A-Z]{2,}(?:\s+[A-Z]{2,})*,\s*[A-Z]{2,}(?:\s+[A-Z]{2,})*?)(?=\s*(?:Nationality|Address|License|PHL|\n\s*PHL))"
    };

            foreach (var pattern in namePatterns)
            {
                var match = Regex.Match(ocrText, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                if (match.Success)
                {
                    var extractedName = match.Groups[1].Value.Trim();

                    // Clean the extracted name
                    extractedName = CleanDriversLicenseName(extractedName);

                    // Validate the name (should not contain department words)
                    if (IsValidDriversLicenseName(extractedName))
                    {
                        result.Name = extractedName;
                        break;
                    }
                }
            }

            // Fallback: If no pattern worked, try to extract manually by looking for comma-separated format
            if (string.IsNullOrEmpty(result.Name))
            {
                var lines = ocrText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();

                    // Look for comma-separated names that don't contain department words
                    if (trimmedLine.Contains(',') && IsValidDriversLicenseName(trimmedLine))
                    {
                        var cleanedName = CleanDriversLicenseName(trimmedLine);
                        if (!string.IsNullOrEmpty(cleanedName))
                        {
                            result.Name = cleanedName;
                            break;
                        }
                    }
                }
            }

            // Extract expiry date for driver's license
            var expiryPatterns = new[]
            {
        @"Exp(?:iry|iration)?\s*Date\s*:?\s*(\d{4}[/-]\d{1,2}[/-]\d{1,2})",
        @"(\d{4}[/-]\d{1,2}[/-]\d{1,2})", // YYYY/MM/DD format common in PH licenses
        @"(\d{1,2}[/-]\d{1,2}[/-]\d{4})" // MM/DD/YYYY format
    };

            foreach (var pattern in expiryPatterns)
            {
                var matches = Regex.Matches(ocrText, pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    var dateString = match.Groups[1].Value;
                    if (DateTime.TryParseExact(dateString, new[] { "yyyy/MM/dd", "yyyy-MM-dd", "MM/dd/yyyy", "M/d/yyyy", "MM-dd-yyyy", "M-d-yyyy" }, null, System.Globalization.DateTimeStyles.None, out var expiryDate))
                    {
                        // Only use future dates as expiry dates
                        if (expiryDate > DateTime.Today)
                        {
                            result.ExpiryDate = expiryDate;
                            break;
                        }
                    }
                }
                if (result.ExpiryDate.HasValue) break;
            }

            return result;
        }

        // Helper method to clean driver's license names
        private string CleanDriversLicenseName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";

            // Remove common OCR artifacts and extra characters
            name = Regex.Replace(name, @"[^\w\s,]", ""); // Keep only letters, spaces, and commas
            name = Regex.Replace(name, @"\s+", " "); // Replace multiple spaces with single space
            name = name.Trim();

            // Remove department-related words that might have been captured
            var departmentWords = new[]
            {
        "DEPARTMENT", "DEPT", "TRANSPORTATION", "LAND", "OFFICE", "REPUBLIC",
        "PHILIPPINES", "ANTEPORTATION", "ANSP", "LTO", "GOVERNMENT"
    };

            var words = name.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            var cleanWords = words.Where(word =>
                !departmentWords.Contains(word.ToUpperInvariant()) &&
                word.Length > 1 && // Remove single letters
                !Regex.IsMatch(word, @"^\d+$") // Remove pure numbers
            ).ToArray();

            // Reconstruct name maintaining comma separation if it was there originally
            if (name.Contains(','))
            {
                var commaIndex = Array.FindIndex(cleanWords, w => name.IndexOf(w) > name.IndexOf(','));
                if (commaIndex > 0)
                {
                    var lastName = string.Join(" ", cleanWords.Take(commaIndex));
                    var firstName = string.Join(" ", cleanWords.Skip(commaIndex));
                    return $"{lastName}, {firstName}".Trim();
                }
            }

            return string.Join(" ", cleanWords);
        }

        // Helper method to validate if extracted text is a valid driver's license name
        private bool IsValidDriversLicenseName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            // Must contain letters
            if (!name.Any(char.IsLetter)) return false;

            // Should not contain department/office words
            var invalidWords = new[]
            {
        "DEPARTMENT", "TRANSPORTATION", "LAND", "OFFICE", "REPUBLIC",
        "PHILIPPINES", "ANTEPORTATION", "LICENSE", "DRIVER", "GOVT"
    };

            var upperName = name.ToUpperInvariant();
            if (invalidWords.Any(word => upperName.Contains(word))) return false;

            // Should be reasonable length (names shouldn't be too long)
            if (name.Length > 50) return false;

            // If it contains comma, should have reasonable format
            if (name.Contains(','))
            {
                var parts = name.Split(',');
                if (parts.Length > 2 || parts.Any(p => p.Trim().Length < 2)) return false;
            }

            return true;
        }

        private IdOcrResult ParsePassport(string ocrText)
        {
            var result = new IdOcrResult();

            // Split lines for more accurate parsing
            var lines = ocrText.Split('\n').Select(l => l.Trim()).ToList();

            // Philippine passport number: P followed by 8 digits and a letter (e.g., P9261295C)
            var idNumberPatterns = new[]
            {
        @"P\d{7}[A-Z]",           // P9261295C format
        @"Passport\s*no\s*[:\.]?\s*(P\d{7}[A-Z])",
        @"Pasaporte\s*blg/Passport\s*no\s*[:\.]?\s*(P\d{7}[A-Z])",
        @"\b(P\d{7}[A-Z])\b"     // Standalone passport number
    };

            foreach (var pattern in idNumberPatterns)
            {
                var match = Regex.Match(ocrText, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    result.IdNumber = match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : match.Value.Trim();
                    break;
                }
            }

            // Name extraction for Philippine passport
            string lastName = "", givenNames = "", middleName = "";

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];

                // Look for "Apelyido/Surname" followed by the surname
                if (Regex.IsMatch(line, @"Apelyido/Surname", RegexOptions.IgnoreCase))
                {
                    if (i + 1 < lines.Count && !string.IsNullOrWhiteSpace(lines[i + 1]))
                    {
                        lastName = CleanPassportName(lines[i + 1]);
                    }
                }

                // Look for "Pangalan/Given names" followed by the given names
                if (Regex.IsMatch(line, @"Pangalan/Given names", RegexOptions.IgnoreCase))
                {
                    if (i + 1 < lines.Count && !string.IsNullOrWhiteSpace(lines[i + 1]))
                    {
                        givenNames = CleanPassportName(lines[i + 1]);
                    }
                }

                // Look for "Panggitnang apelyido/Middle name" followed by the middle name
                if (Regex.IsMatch(line, @"Panggitnang apelyido/Middle name", RegexOptions.IgnoreCase))
                {
                    if (i + 1 < lines.Count && !string.IsNullOrWhiteSpace(lines[i + 1]))
                    {
                        middleName = CleanPassportName(lines[i + 1]);
                    }
                }
            }

            // If structured extraction didn't work, try alternative patterns
            if (string.IsNullOrEmpty(lastName) && string.IsNullOrEmpty(givenNames))
            {
                // Try to extract from MRZ (Machine Readable Zone) format
                var mrzMatch = Regex.Match(ocrText, @"P<PHL([A-Z]+)<<([A-Z]+)<+", RegexOptions.IgnoreCase);
                if (mrzMatch.Success)
                {
                    lastName = mrzMatch.Groups[1].Value;
                    givenNames = mrzMatch.Groups[2].Value;
                }
                else
                {
                    // Fallback: Look for capitalized names near passport-specific keywords
                    var namePatterns = new[]
                    {
        @"([A-Z]{3,}(?:\s+[A-Z]{3,})*)\s*\n\s*([A-Z]{3,}(?:\s+[A-Z]{3,})*)", // First capitalized word(s) followed by second capitalized word(s)
        @"([A-Z]{3,})\s*\n\s*([A-Z]{3,}(?:\s+[A-Z]{3,})*)"                   // Single surname followed by given names
    };

                    foreach (var pattern in namePatterns)
                    {
                        var match = Regex.Match(ocrText, pattern, RegexOptions.Multiline);
                        if (match.Success && IsValidPassportName(match.Groups[1].Value))
                        {
                            // Determine which is surname and which is given name based on context
                            if (string.IsNullOrEmpty(lastName))
                            {
                                lastName = match.Groups[1].Value.Trim();
                                if (match.Groups.Count > 2)
                                    givenNames = match.Groups[2].Value.Trim();
                            }
                            break;
                        }
                    }
                }
            }

            // Construct full name in Philippine format (Given Middle Last)
            var nameParts = new List<string>();
            if (!string.IsNullOrEmpty(givenNames)) nameParts.Add(givenNames);
            if (!string.IsNullOrEmpty(middleName)) nameParts.Add(middleName);
            if (!string.IsNullOrEmpty(lastName)) nameParts.Add(lastName);

            result.Name = string.Join(" ", nameParts).Trim();

            // If we still don't have a name in the correct format, but we have lastName and givenNames
            // Create the comma-separated format for consistency with other documents
            if (string.IsNullOrEmpty(result.Name) && !string.IsNullOrEmpty(lastName) && !string.IsNullOrEmpty(givenNames))
            {
                result.Name = $"{lastName}, {givenNames}";
            }

            // Extract expiry date for Philippine passport
            var expiryPatterns = new[]
            {
        @"Petsa\s*ng\s*pagkawalang\s*bisa/Valid\s*until\s*[:\.]?\s*(\d{1,2}\s+[A-Z]{3}\s+\d{4})", // "13 MAR 2035"
        @"Valid\s*until\s*[:\.]?\s*(\d{1,2}\s+[A-Z]{3}\s+\d{4})",
        @"(\d{1,2}\s+MAR\s+\d{4})", // Specific format like "13 MAR 2035"
        @"(\d{1,2}\s+[A-Z]{3}\s+\d{4})", // General format "DD MMM YYYY"
        @"(\d{1,2}[/-]\d{1,2}[/-]\d{4})" // Fallback numeric format
    };

            foreach (var pattern in expiryPatterns)
            {
                var matches = Regex.Matches(ocrText, pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    var dateString = match.Groups[1].Value.Trim();

                    // Try different date formats
                    var dateFormats = new[]
                    {
                "d MMM yyyy",    // "13 MAR 2035"
                "dd MMM yyyy",   // "13 MAR 2035"
                "d/M/yyyy",      // "13/3/2035"
                "dd/MM/yyyy",    // "13/03/2035"
                "MM/dd/yyyy",    // "03/13/2035"
                "d-M-yyyy",      // "13-3-2035"
                "dd-MM-yyyy",    // "13-03-2035"
                "MM-dd-yyyy"     // "03-13-2035"
            };

                    if (DateTime.TryParseExact(dateString, dateFormats, null, System.Globalization.DateTimeStyles.None, out var expiryDate))
                    {
                        // Only use future dates as expiry dates
                        if (expiryDate > DateTime.Today)
                        {
                            result.ExpiryDate = expiryDate;
                            break;
                        }
                    }
                }
                if (result.ExpiryDate.HasValue) break;
            }

            // Log the extracted information for debugging
            _logger.LogInformation("Passport parsing results - Number: '{IdNumber}', Name: '{Name}', Expiry: '{ExpiryDate}'",
                result.IdNumber, result.Name, result.ExpiryDate);

            return result;
        }

        // Helper method to clean passport names
        private string CleanPassportName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";

            // Remove common OCR artifacts and extra characters
            name = Regex.Replace(name, @"[^\w\s]", ""); // Keep only letters, spaces
            name = Regex.Replace(name, @"\s+", " "); // Replace multiple spaces with single space
            name = name.Trim().ToUpperInvariant();

            // Remove common passport header words that might be captured
            var headerWords = new[]
            {
        "REPUBLIKA", "PILIPINAS", "REPUBLIC", "PHILIPPINES", "PASSPORT", "PASAPORTE",
        "APELYIDO", "SURNAME", "PANGALAN", "GIVEN", "NAMES", "PANGGITNANG",
        "MIDDLE", "NAME", "TYPE", "KODIGO", "COUNTRY", "CODE"
    };

            var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var cleanWords = words.Where(word =>
                !headerWords.Contains(word) &&
                word.Length > 1 && // Remove single letters
                !Regex.IsMatch(word, @"^\d+$") // Remove pure numbers
            ).ToArray();

            return string.Join(" ", cleanWords);
        }

        // Helper method to validate if extracted text is a valid passport name
        private bool IsValidPassportName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            // Must contain letters
            if (!name.Any(char.IsLetter)) return false;

            // Should not contain passport header words
            var invalidWords = new[]
            {
        "REPUBLIKA", "PILIPPINAS", "REPUBLIC", "PHILIPPINES", "PASSPORT", "PASAPORTE",
        "TYPE", "KODIGO", "COUNTRY", "CODE", "PETSA", "DATE", "BIRTH", "KASARIAN",
        "SEX", "LUGAR", "PLACE", "PAGKAWALANG", "VALID", "UNTIL", "AUTHORITY"
    };

            var upperName = name.ToUpperInvariant();
            if (invalidWords.Any(word => upperName.Contains(word))) return false;

            // Should be reasonable length (names shouldn't be too long or too short)
            if (name.Length > 50 || name.Length < 3) return false;

            // Should not be mostly numbers
            var letterCount = name.Count(char.IsLetter);
            var totalCount = name.Replace(" ", "").Length;
            if (letterCount < totalCount * 0.7) return false; // At least 70% letters

            return true;
        }

        private IdOcrResult ParseGenericId(string ocrText)
        {
            var result = new IdOcrResult();

            // Generic ID number extraction - look for common patterns
            var idPatterns = new[]
            {
        @"\b\d{4}-\d{4}-\d{4}-\d{4}\b", // ####-####-####-####
        @"\b[A-Z]\d{2}-\d{2}-\d{5}\b",  // N##-##-#####
        @"\b[A-Z0-9]{8,12}\b"           // Alphanumeric 8-12 chars
    };

            foreach (var pattern in idPatterns)
            {
                var match = Regex.Match(ocrText, pattern);
                if (match.Success)
                {
                    result.IdNumber = match.Value.Trim();
                    break;
                }
            }

            // Generic name extraction - look for comma-separated names or capitalized words
            var namePatterns = new[]
            {
        @"([A-Z][a-z]+\s*,\s*[A-Z][a-z\s]+)", // "Surname, Given names"
        @"([A-Z][A-Z\s]+,[A-Z\s]+)"          // "SURNAME, GIVEN NAMES"
    };

            foreach (var pattern in namePatterns)
            {
                var match = Regex.Match(ocrText, pattern);
                if (match.Success)
                {
                    result.Name = match.Groups[1].Value.Trim();
                    break;
                }
            }

            // Try to extract expiry date for generic IDs
            var expiryPatterns = new[]
            {
        @"Exp(?:iry|ires?)?\s*:?\s*(\d{1,2}[/-]\d{1,2}[/-]\d{2,4})",
        @"Valid\s*Until\s*:?\s*(\d{1,2}[/-]\d{1,2}[/-]\d{2,4})",
        @"(\d{1,2}[/-]\d{1,2}[/-]\d{2,4})" // Generic date pattern
    };

            foreach (var pattern in expiryPatterns)
            {
                var matches = Regex.Matches(ocrText, pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    var dateString = match.Groups[1].Value;
                    if (DateTime.TryParseExact(dateString, new[] { "MM/dd/yyyy", "M/d/yyyy", "MM-dd-yyyy", "M-d-yyyy", "dd/MM/yyyy", "d/M/yyyy" }, null, System.Globalization.DateTimeStyles.None, out var expiryDate))
                    {
                        if (expiryDate > DateTime.Today)
                        {
                            result.ExpiryDate = expiryDate;
                            break;
                        }
                    }
                }
                if (result.ExpiryDate.HasValue) break;
            }

            return result;
        }

        /// <summary>
        /// Cross-matches the extracted name from ID document with the registered user's first and last name
        /// </summary>
        /// <param name="extractedName">Name extracted from ID document</param>
        /// <param name="userId">User ID to look up registered name</param>
        /// <returns>Tuple indicating if names match and message</returns>
        private async Task<(bool match, string message)> CrossMatchNameWithRegisteredUserAsync(string extractedName, string userId)
        {
            try
            {
                // Get user's registered name from database
                var user = await _context.Users
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.FirstName, u.LastName })
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    _logger.LogWarning("User not found for ID matching: {UserId}", userId);
                    return (false, "User account not found. Please contact support.");
                }

                var registeredFirstName = user.FirstName?.Trim() ?? "";
                var registeredLastName = user.LastName?.Trim() ?? "";

                if (string.IsNullOrEmpty(registeredFirstName) || string.IsNullOrEmpty(registeredLastName))
                {
                    _logger.LogWarning("Incomplete user name data for ID matching: {UserId}", userId);
                    return (false, "User profile is incomplete. Please update your first and last name in your profile.");
                }

                // Normalize and clean the extracted name
                var normalizedExtractedName = NormalizeName(extractedName);
                var normalizedRegisteredName = NormalizeName($"{registeredFirstName} {registeredLastName}");

                _logger.LogInformation("Name matching - Extracted: '{ExtractedName}' (normalized: '{NormalizedExtracted}'), Registered: '{RegisteredName}' (normalized: '{NormalizedRegistered}')",
                    extractedName, normalizedExtractedName, $"{registeredFirstName} {registeredLastName}", normalizedRegisteredName);

                // Check for exact match
                if (normalizedExtractedName.Equals(normalizedRegisteredName, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Name match successful for user {UserId}", userId);
                    return (true, "Name verification successful");
                }

                // Check for partial match (handles cases where middle names might be missing or extra)
                var extractedParts = normalizedExtractedName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var registeredParts = normalizedRegisteredName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                // Check if first and last names match (ignoring middle names)
                if (extractedParts.Length >= 2 && registeredParts.Length >= 2)
                {
                    var extractedFirst = extractedParts[0];
                    var extractedLast = extractedParts[extractedParts.Length - 1];
                    var registeredFirst = registeredParts[0];
                    var registeredLast = registeredParts[registeredParts.Length - 1];

                    if (extractedFirst.Equals(registeredFirst, StringComparison.OrdinalIgnoreCase) &&
                        extractedLast.Equals(registeredLast, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Partial name match successful for user {UserId} (first and last names match)", userId);
                        return (true, "Name verification successful (first and last names match)");
                    }
                }

                // Check for reverse order (Last, First format)
                if (extractedParts.Length >= 2)
                {
                    var extractedFirst = extractedParts[extractedParts.Length - 1]; // Last part as first name
                    var extractedLast = extractedParts[0]; // First part as last name
                    var registeredFirst = registeredParts[0];
                    var registeredLast = registeredParts[registeredParts.Length - 1];

                    if (extractedFirst.Equals(registeredFirst, StringComparison.OrdinalIgnoreCase) &&
                        extractedLast.Equals(registeredLast, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Reverse order name match successful for user {UserId}", userId);
                        return (true, "Name verification successful (reverse order match)");
                    }
                }

                _logger.LogWarning("Name mismatch for user {UserId}. Extracted: '{ExtractedName}', Registered: '{RegisteredName}'",
                    userId, extractedName, $"{registeredFirstName} {registeredLastName}");

                return (false, $"Name on ID document does not match your registered name. ID shows: '{extractedName}', but your account shows: '{registeredFirstName} {registeredLastName}'. Please ensure you are using your own ID document or update your profile information.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during name matching for user {UserId}", userId);
                return (false, "Error verifying name match. Please try again or contact support.");
            }
        }

        /// <summary>
        /// Normalizes a name for comparison by removing extra spaces, converting to uppercase, and handling common variations
        /// </summary>
        /// <param name="name">Name to normalize</param>
        /// <returns>Normalized name</returns>
        private string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "";

            // Remove extra whitespace and convert to uppercase
            var normalized = name.Trim().ToUpperInvariant();

            // Replace multiple spaces with single space
            normalized = Regex.Replace(normalized, @"\s+", " ");

            // Remove common punctuation and special characters
            normalized = Regex.Replace(normalized, @"[^\w\s]", "");

            // Handle common name variations and abbreviations
            var replacements = new Dictionary<string, string>
            {
                { @"\bJR\b", "JUNIOR" },
                { @"\bSR\b", "SENIOR" },
                { @"\bIII\b", "THIRD" },
                { @"\bII\b", "SECOND" },
                { @"\bDELA\b", "DE LA" },
                { @"\bDELOS\b", "DE LOS" },
                { @"\bDEL\b", "DE" },
                { @"\bVAN\b", "VAN" },
                { @"\bVON\b", "VON" }
            };

            foreach (var replacement in replacements)
            {
                normalized = Regex.Replace(normalized, replacement.Key, replacement.Value, RegexOptions.IgnoreCase);
            }

            return normalized.Trim();
        }

        // Helper method to check if a word is likely a name (not address, date, or header)
        private bool IsLikelyName(string word)
        {
            if (string.IsNullOrEmpty(word) || word.Length < 3) return false;

            // Exclude header words
            var headerWords = new[] {
                "REPUBLIKA", "PILIPINAS", "PAMBANSANG", "PAGKAKAKILANLAN",
                "PHILIPPINE", "STATISTICS", "AUTHORITY", "PSA", "PHL",
                "IDENTIFICATION", "CARD", "REPUBLIC", "PHILIPPINES", "SEATORTIC"
            };
            if (headerWords.Contains(word)) return false;

            // Exclude months
            var months = new[] { "JANUARY", "FEBRUARY", "MARCH", "APRIL", "MAY", "JUNE",
                        "JULY", "AUGUST", "SEPTEMBER", "OCTOBER", "NOVEMBER", "DECEMBER" };
            if (months.Contains(word)) return false;

            // Exclude common address words
            var addressWords = new[] { "STREET", "ST", "AVENUE", "AVE", "ROAD", "RD",
                              "CITY", "PROVINCE", "DISTRICT", "BARANGAY", "BRGY" };
            if (addressWords.Contains(word)) return false;

            // Exclude numbers
            if (Regex.IsMatch(word, @"^\d+$")) return false;

            return true;
        }

        // Helper method to clean up extracted names
        private string CleanName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";

            // Remove common OCR artifacts and extra characters
            name = Regex.Replace(name, @"[^A-Z\s]", ""); // Keep only letters and spaces
            name = Regex.Replace(name, @"\s+", " "); // Replace multiple spaces with single space
            name = name.Trim();

            // Remove single letters that are likely OCR artifacts
            var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var cleanWords = words.Where(word => word.Length > 1).ToArray();

            return string.Join(" ", cleanWords);
        }
    }
}