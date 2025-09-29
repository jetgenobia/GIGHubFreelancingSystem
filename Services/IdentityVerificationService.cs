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

                // Verify ID Document
                if (model.IdDocumentImage != null)
                {
                    var (idVerified, idMessage, idConfidence, extractedName, extractedIdNumber) = await VerifyIdDocumentAsync(
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
            string? storedIdDocumentImageBase64 = null) // new param
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
                    StoredIdDocumentImageData = storedIdDocumentImageBase64 // <-- pass session image here
                };

                // Save verification data with encryption (will handle stored base64)
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

        public async Task<(bool verified, string message, float confidence, string? extractedIdName, string? extractedIdNumber)> VerifyIdDocumentAsync(
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
                    return (false, "No document image provided for verification.", 0.0f, null, null);
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
                        var visionResponse = System.Text.Json.JsonSerializer.Deserialize<dynamic>(responseContent);
                        var textAnnotations = visionResponse.GetProperty("responses")[0].GetProperty("textAnnotations");

                        var extractedText = "";
                        if (textAnnotations.GetArrayLength() > 0)
                        {
                            extractedText = textAnnotations[0].GetProperty("description").GetString();
                        }

                        // Parse the extracted text based on document type
                        IdOcrResult ocrResult = null;
                        if (idDocumentType == "National ID")
                            ocrResult = ParseNationalId(extractedText);
                        else if (idDocumentType == "Driver's License")
                            ocrResult = ParseDriversLicense(extractedText);
                        else if (idDocumentType == "Passport")
                            ocrResult = ParsePassport(extractedText);
                        else
                            ocrResult = ParseGenericId(extractedText);

                        // Basic validation - check if it looks like an ID document
                        var hasNumbers = extractedText.Any(char.IsDigit);
                        var hasLetters = extractedText.Any(char.IsLetter);
                        var hasDatePattern = System.Text.RegularExpressions.Regex.IsMatch(extractedText, @"\d{1,2}[/-]\d{1,2}[/-]\d{2,4}");

                        // Log the OCR results for debugging
                        _logger.LogInformation("OCR Results for {DocumentType}: Name='{ExtractedName}', ID='{ExtractedId}'",
                            idDocumentType, ocrResult?.Name, ocrResult?.IdNumber);

                        // ID documents need at least numbers and letters
                        if (hasNumbers && hasLetters && ocrResult != null)
                        {
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
                                    return (false, nameMatchMessage, 0.0f, ocrResult.Name, ocrResult.IdNumber);
                                }
                                else
                                {
                                    _logger.LogInformation("Name match successful for user {UserId}", userId);
                                    message = "ID document verified with name match";
                                }
                            }

                            return (true, message, confidence, ocrResult.Name, ocrResult.IdNumber);
                        }

                        return (false, "Unable to extract required information from ID document. Please ensure the image is clear and contains readable text.", 0.0f, null, null);
                    }
                    else
                    {
                        _logger.LogError("Google Vision API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                        return (false, "Error processing ID document with Google Vision API.", 0.0f, null, null);
                    }
                }
                else
                {
                    return (false, "Google Cloud Vision API key not configured.", 0.0f, null, null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying ID document for user {UserId}", userId);
                return (false, "Error processing ID document. Please try again.", 0.0f, null, null);
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

                verification.IdDocumentExpiryDate = model.IdDocumentHasNoExpiration
                    ? DateTime.UtcNow.AddYears(100)
                    : model.IdDocumentExpiryDate;
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

                    verification.IdDocumentExpiryDate = model.IdDocumentHasNoExpiration
                        ? DateTime.UtcNow.AddYears(100)
                        : model.IdDocumentExpiryDate;
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
                verification.IdDocumentExpiryDate = model.IdDocumentHasNoExpiration
                    ? DateTime.UtcNow.AddYears(100)
                    : model.IdDocumentExpiryDate;
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

            _logger.LogInformation("Final parsed name: {FullName}", result.Name);

            return result;
        }

        private IdOcrResult ParseDriversLicense(string ocrText)
        {
            var result = new IdOcrResult();

            // License No.: N##-##-#####
            var idNumberMatch = Regex.Match(ocrText, @"License No\.?\s*([A-Z0-9\-]+)");
            result.IdNumber = idNumberMatch.Success ? idNumberMatch.Groups[1].Value.Trim() : null;

            // Name: Dela Cruz, Juan Pedro Garcia (all uppercase, comma separated)
            var nameMatch = Regex.Match(ocrText, @"([A-Z\s]+,[A-Z\s]+)");
            result.Name = nameMatch.Success ? nameMatch.Groups[1].Value.Trim() : null;

            return result;
        }

        private IdOcrResult ParsePassport(string ocrText)
        {
            var result = new IdOcrResult();

            // Passport number: Usually starts with P followed by numbers
            var idNumberMatch = Regex.Match(ocrText, @"(?:Passport\s*No\.?|P)\s*([A-Z0-9]+)");
            result.IdNumber = idNumberMatch.Success ? idNumberMatch.Groups[1].Value.Trim() : null;

            // Name extraction for passport - typically "Surname, Given names"
            var nameMatch = Regex.Match(ocrText, @"([A-Z\s]+,[A-Z\s]+)");
            result.Name = nameMatch.Success ? nameMatch.Groups[1].Value.Trim() : null;

            return result;
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