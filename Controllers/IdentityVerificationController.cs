using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Freelancing.Models;
using Freelancing.Services;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.IO;

namespace Freelancing.Controllers
{
    [Authorize]
    public class IdentityVerificationController : Controller
    {
        private readonly IIdentityVerificationService _verificationService;
        private readonly ILogger<IdentityVerificationController> _logger;
        private readonly IIdentityEncryptionService _encryptionService;

        public IdentityVerificationController(
            IIdentityVerificationService verificationService,
            ILogger<IdentityVerificationController> logger,
            IIdentityEncryptionService encryptionService)
        {
            _verificationService = verificationService;
            _logger = logger;
            _encryptionService = encryptionService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return RedirectToAction("Login", "Account");
                }

                // Check user's verification status
                var verificationStatus = await _verificationService.GetVerificationStatusAsync(userId);

                // If user has a pending verification, redirect to Status page
                if (verificationStatus != null && verificationStatus.Status.Equals("PENDING", StringComparison.OrdinalIgnoreCase))
                {
                    return RedirectToAction("Status");
                }

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking verification status in Index action for user {UserId}", GetCurrentUserId());
                return View(); // Show the Index view even if there's an error checking status
            }
        }

        [HttpGet]
        public IActionResult Document()
        {
            var model = new DocumentVerificationViewModel();

            var sessionDocumentData = HttpContext.Session.GetString("DocumentData");
            if (!string.IsNullOrEmpty(sessionDocumentData))
            {
                try
                {
                    var documentData = System.Text.Json.JsonSerializer.Deserialize<DocumentVerificationData>(sessionDocumentData);

                    // Always restore the model from session data
                    model.IdDocumentType = documentData.IdDocumentType;
                    model.ExtractedIdNumber = documentData.ExtractedIdNumber;
                    model.IdDocumentExpiryDate = documentData.IdDocumentExpiryDate;
                    model.IdDocumentHasNoExpiration = documentData.IdDocumentHasNoExpiration;

                    // Check if image data exists in session (now Base64 only)
                    if (!string.IsNullOrEmpty(documentData.IdDocumentImageData))
                    {
                        ViewBag.StoredImageData = documentData.IdDocumentImageData;
                        ViewBag.StoredImageContentType = documentData.IdDocumentImageContentType;
                        ViewBag.HasStoredImage = true;

                        _logger.LogInformation(
                            "Document GET: Restored session data for user. Has extracted ID: {HasExtractedId}",
                            !string.IsNullOrEmpty(documentData.ExtractedIdNumber));
                    }
                    else
                    {
                        ViewBag.HasStoredImage = false;
                        _logger.LogInformation("Document GET: No image data found in session");
                    }

                    // Set additional ViewBag properties for the view
                    ViewBag.ExtractedIdName = documentData.ExtractedIdName;
                    ViewBag.HasExtractedData = !string.IsNullOrEmpty(documentData.ExtractedIdNumber);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deserializing document data from session in Document GET");
                    HttpContext.Session.Remove("DocumentData");
                    ViewBag.HasStoredImage = false;
                    ViewBag.HasExtractedData = false;
                }
            }
            else
            {
                ViewBag.HasStoredImage = false;
                ViewBag.HasExtractedData = false;
                _logger.LogInformation("Document GET: No session data found, showing fresh form");
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Document(DocumentVerificationViewModel model)
        {
            // Restore ViewBag data for validation failures
            var sessionData = HttpContext.Session.GetString("DocumentData");
            DocumentVerificationData existingDocumentData = null;

            if (!string.IsNullOrEmpty(sessionData))
            {
                try
                {
                    existingDocumentData = System.Text.Json.JsonSerializer.Deserialize<DocumentVerificationData>(sessionData);

                    // Restore model data that might be missing from form submission
                    if (string.IsNullOrEmpty(model.ExtractedIdNumber) && !string.IsNullOrEmpty(existingDocumentData.ExtractedIdNumber))
                    {
                        model.ExtractedIdNumber = existingDocumentData.ExtractedIdNumber;
                    }

                    // Always restore ViewBag data
                    if (!string.IsNullOrEmpty(existingDocumentData.IdDocumentImageData))
                    {
                        ViewBag.StoredImageData = existingDocumentData.IdDocumentImageData;
                        ViewBag.StoredImageContentType = existingDocumentData.IdDocumentImageContentType;
                        ViewBag.HasStoredImage = true;
                    }

                    ViewBag.ExtractedIdName = existingDocumentData.ExtractedIdName;
                    ViewBag.HasExtractedData = !string.IsNullOrEmpty(existingDocumentData.ExtractedIdNumber);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deserializing document data during model restoration");
                    ViewBag.HasStoredImage = false;
                    ViewBag.HasExtractedData = false;
                }
            }

            // Enhanced validation logic
            bool isReturningUser = !string.IsNullOrEmpty(model.ExtractedIdNumber) ||
                                  (existingDocumentData != null && !string.IsNullOrEmpty(existingDocumentData.ExtractedIdNumber));
            bool hasStoredImage = existingDocumentData != null && !string.IsNullOrEmpty(existingDocumentData.IdDocumentImageData);
            bool hasNewUpload = model.IdDocumentImage != null && model.IdDocumentImage.Length > 0;

            // Custom validation for file upload requirement
            if (!isReturningUser || (!hasStoredImage && !hasNewUpload))
            {
                if (!hasNewUpload)
                {
                    ModelState.AddModelError("IdDocumentImage", "Please upload your ID document.");
                }
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return RedirectToAction("Login", "Account");
                }

                DateTime? expiryDate = null;
                if (!model.IdDocumentHasNoExpiration && model.IdDocumentExpiryDate.HasValue)
                {
                    expiryDate = model.IdDocumentExpiryDate.Value;
                }

                DocumentVerificationData documentData;

                if (existingDocumentData != null)
                {
                    // Update existing data
                    documentData = existingDocumentData;

                    bool hasFormChanges =
                        documentData.IdDocumentType != model.IdDocumentType ||
                        documentData.IdDocumentExpiryDate != expiryDate ||
                        documentData.IdDocumentHasNoExpiration != model.IdDocumentHasNoExpiration;

                    if (hasFormChanges || hasNewUpload)
                    {
                        // Update form data
                        documentData.IdDocumentType = model.IdDocumentType;
                        documentData.IdDocumentExpiryDate = expiryDate;
                        documentData.IdDocumentHasNoExpiration = model.IdDocumentHasNoExpiration;

                        if (hasNewUpload)
                        {
                            // Validate file type and size
                            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".pdf" };
                            var fileExtension = Path.GetExtension(model.IdDocumentImage.FileName).ToLowerInvariant();

                            if (!allowedExtensions.Contains(fileExtension))
                            {
                                ModelState.AddModelError("IdDocumentImage", $"File type not allowed. Allowed types: {string.Join(", ", allowedExtensions)}");
                                return View(model);
                            }

                            if (model.IdDocumentImage.Length > 10 * 1024 * 1024) // 10MB limit
                            {
                                ModelState.AddModelError("IdDocumentImage", "File size must be less than 10MB.");
                                return View(model);
                            }

                            try
                            {
                                // Convert uploaded file to Base64 for session storage
                                using var memoryStream = new MemoryStream();
                                await model.IdDocumentImage.CopyToAsync(memoryStream);
                                var imageBytes = memoryStream.ToArray();
                                var base64String = Convert.ToBase64String(imageBytes);

                                documentData.IdDocumentImageData = $"data:{model.IdDocumentImage.ContentType};base64,{base64String}";
                                documentData.IdDocumentImageContentType = model.IdDocumentImage.ContentType;

                                _logger.LogInformation("Document converted to Base64 for user {UserId}", userId);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to process identity document for user {UserId}", userId);
                                ModelState.AddModelError("IdDocumentImage", "Failed to process document. Please try again.");
                                return View(model);
                            }

                            try
                            {
                                var documentResult = await _verificationService.VerifyIdDocumentAsync(
                                    model.IdDocumentImage,
                                    model.IdDocumentType,
                                    null,
                                    expiryDate,
                                    model.IdDocumentHasNoExpiration,
                                    userId
                                );

                                documentData.ExtractedIdName = documentResult.extractedIdName;
                                documentData.ExtractedIdNumber = documentResult.extractedIdNumber;
                                documentData.IdDocumentVerified = documentResult.verified;
                                documentData.IdDocumentConfidence = documentResult.confidence;
                                documentData.IdDocumentMessage = documentResult.message;

                                _logger.LogInformation(
                                    "New document uploaded and processed for returning user {UserId}. Extracted name: {ExtractedName}, Extracted ID: {ExtractedId}",
                                    userId, documentResult.extractedIdName, documentResult.extractedIdNumber);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error during document re-verification for user {UserId}", userId);
                                documentData.IdDocumentMessage = "Document uploaded but extraction failed. Please verify manually.";
                            }
                        }
                        else
                        {
                            // Form data changed but no new image - preserve existing extraction data
                            documentData.IdDocumentMessage = "Document data updated, using existing document image";
                        }

                        HttpContext.Session.SetString("DocumentData", System.Text.Json.JsonSerializer.Serialize(documentData));
                        _logger.LogInformation(
                            "Updated document data for returning user {UserId}. Has image: {HasImage}, Has extracted ID: {HasExtractedId}",
                            userId, !string.IsNullOrEmpty(documentData.IdDocumentImageData), !string.IsNullOrEmpty(documentData.ExtractedIdNumber));
                    }
                }
                else
                {
                    // Create new document data
                    documentData = new DocumentVerificationData
                    {
                        IdDocumentType = model.IdDocumentType,
                        ExtractedIdNumber = null,
                        IdDocumentExpiryDate = expiryDate,
                        IdDocumentHasNoExpiration = model.IdDocumentHasNoExpiration,
                        IdDocumentVerified = false,
                        IdDocumentConfidence = 0.0f,
                        IdDocumentMessage = "Pending verification",
                        IdDocumentImageData = null,
                        IdDocumentImageContentType = null
                    };

                    // Validate file type and size
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".pdf" };
                    var fileExtension = Path.GetExtension(model.IdDocumentImage.FileName).ToLowerInvariant();

                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        ModelState.AddModelError("IdDocumentImage", $"File type not allowed. Allowed types: {string.Join(", ", allowedExtensions)}");
                        return View(model);
                    }

                    if (model.IdDocumentImage.Length > 10 * 1024 * 1024) // 10MB limit
                    {
                        ModelState.AddModelError("IdDocumentImage", "File size must be less than 10MB.");
                        return View(model);
                    }

                    try
                    {
                        // Convert uploaded file to Base64 for session storage
                        using var memoryStream = new MemoryStream();
                        await model.IdDocumentImage.CopyToAsync(memoryStream);
                        var imageBytes = memoryStream.ToArray();
                        var base64String = Convert.ToBase64String(imageBytes);

                        documentData.IdDocumentImageData = $"data:{model.IdDocumentImage.ContentType};base64,{base64String}";
                        documentData.IdDocumentImageContentType = model.IdDocumentImage.ContentType;

                        _logger.LogInformation("Document converted to Base64 for new user {UserId}", userId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process identity document for user {UserId}", userId);
                        ModelState.AddModelError("IdDocumentImage", "Failed to process document. Please try again.");
                        return View(model);
                    }

                    try
                    {
                        var documentResult = await _verificationService.VerifyIdDocumentAsync(
                            model.IdDocumentImage,
                            model.IdDocumentType,
                            null,
                            expiryDate,
                            model.IdDocumentHasNoExpiration,
                            userId
                        );

                        documentData.ExtractedIdName = documentResult.extractedIdName;
                        documentData.ExtractedIdNumber = documentResult.extractedIdNumber;
                        documentData.IdDocumentVerified = documentResult.verified;
                        documentData.IdDocumentConfidence = documentResult.confidence;
                        documentData.IdDocumentMessage = documentResult.message;

                        _logger.LogInformation(
                            "Document verification completed for new user {UserId}. Extracted name: {ExtractedName}, Extracted ID: {ExtractedId}",
                            userId, documentResult.extractedIdName, documentResult.extractedIdNumber);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during document verification for user {UserId}", userId);
                        documentData.IdDocumentMessage = "Document uploaded but extraction failed. Please verify manually.";
                    }

                    HttpContext.Session.SetString("DocumentData", System.Text.Json.JsonSerializer.Serialize(documentData));
                }

                return RedirectToAction("Verify");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during document verification");
                ModelState.AddModelError("", "An error occurred during document verification. Please try again.");
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult Verify()
        {
            var sessionDocumentData = HttpContext.Session.GetString("DocumentData");
            if (!string.IsNullOrEmpty(sessionDocumentData))
            {
                try
                {
                    var documentData = System.Text.Json.JsonSerializer.Deserialize<DocumentVerificationData>(sessionDocumentData);
                    var model = new FaceVerificationViewModel
                    {
                        IdDocumentType = documentData.IdDocumentType,
                        ExtractedIdNumber = documentData.ExtractedIdNumber,
                        IdDocumentExpiryDate = documentData.IdDocumentExpiryDate,
                        IdDocumentHasNoExpiration = documentData.IdDocumentHasNoExpiration,
                        ExtractedIdName = documentData.ExtractedIdName
                    };

                    // Log verification data for debugging
                    _logger.LogInformation(
                        "Verify GET: Loaded session data. ID Type: {IdType}, Extracted ID: {ExtractedId}, Extracted Name: {ExtractedName}",
                        documentData.IdDocumentType, documentData.ExtractedIdNumber, documentData.ExtractedIdName);

                    return View(model);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deserializing document data from Session in Verify GET");
                    TempData["ErrorMessage"] = "Session data corrupted. Please start the verification process again.";
                    return RedirectToAction("Document");
                }
            }

            _logger.LogWarning("No session document data found in Verify GET, redirecting to Document");
            TempData["ErrorMessage"] = "No document data found. Submitted ID doesn't match with selected ID type.";
            return RedirectToAction("Document");
        }

        [HttpPost]
        public async Task<IActionResult> Verify(FaceVerificationViewModel model)
        {
            _logger.LogInformation("Verify POST action called");
            _logger.LogInformation("LiveFaceImageData length: {Length}",
                model.LiveFaceImageData?.Length ?? 0);
            _logger.LogInformation("AgreeToTerms: {AgreeToTerms}", model.AgreeToTerms);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model state is invalid");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    _logger.LogWarning("Model error: {Error}", error.ErrorMessage);
                }
                return View(model);
            }

            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("User ID is empty, redirecting to login");
                    return RedirectToAction("Login", "Account");
                }

                var sessionDocumentData = HttpContext.Session.GetString("DocumentData");
                if (string.IsNullOrEmpty(sessionDocumentData))
                {
                    _logger.LogWarning("No session document data found");
                    ModelState.AddModelError("", "Document data not found. Please start over from the document verification step.");
                    return RedirectToAction("Document");
                }

                var documentData = System.Text.Json.JsonSerializer.Deserialize<DocumentVerificationData>(sessionDocumentData);

                // Validate essential document data
                if (string.IsNullOrEmpty(documentData.IdDocumentType) ||
                    string.IsNullOrEmpty(documentData.ExtractedIdNumber) ||
                    string.IsNullOrEmpty(documentData.IdDocumentImageData))
                {
                    _logger.LogError(
                        "Incomplete document data. Type: {Type}, ExtractedId: {ExtractedId}, HasImageData: {HasImage}",
                        documentData.IdDocumentType,
                        documentData.ExtractedIdNumber,
                        !string.IsNullOrEmpty(documentData.IdDocumentImageData));

                    ModelState.AddModelError("", "Incomplete document data found. Please restart the verification process.");
                    return RedirectToAction("Document");
                }

                // Verify face first
                _logger.LogInformation("Starting face verification for user {UserId}", userId);
                var faceResult = await _verificationService.VerifyLiveFaceAsync(model.LiveFaceImageData, userId);

                if (!faceResult.verified)
                {
                    _logger.LogWarning("Face verification failed for user {UserId}: {Message}", userId, faceResult.message);
                    ModelState.AddModelError("", $"Face verification failed: {faceResult.message}");

                    // Restore model data for the view
                    model.IdDocumentType = documentData.IdDocumentType;
                    model.ExtractedIdNumber = documentData.ExtractedIdNumber;
                    model.ExtractedIdName = documentData.ExtractedIdName;
                    model.IdDocumentExpiryDate = documentData.IdDocumentExpiryDate;
                    model.IdDocumentHasNoExpiration = documentData.IdDocumentHasNoExpiration;

                    return View(model);
                }

                _logger.LogInformation("Face verification successful, proceeding with complete verification");

                // Complete the verification process
                var result = await _verificationService.CompleteVerificationAsync(
                    model.LiveFaceImageData,
                    userId,
                    documentData.IdDocumentType,
                    documentData.ExtractedIdNumber,
                    documentData.IdDocumentExpiryDate,
                    documentData.IdDocumentHasNoExpiration,
                    documentData.IdDocumentVerified,
                    documentData.IdDocumentConfidence,
                    documentData.ExtractedIdName,
                    documentData.IdDocumentImageData // Now contains Base64 data instead of URL
                );

                if (result.Success)
                {
                    _logger.LogInformation("Identity verification completed successfully for user {UserId}", userId);

                    // Clear session data after successful verification
                    HttpContext.Session.Remove("DocumentData");
                    TempData["SuccessMessage"] = "Identity verification completed successfully!";
                    return RedirectToAction("Status");
                }
                else
                {
                    _logger.LogWarning("Identity verification failed for user {UserId}: {Message}", userId, result.Message);

                    var errorMessage = !string.IsNullOrEmpty(result.Message) ? result.Message : "Verification failed. Please try again.";
                    ModelState.AddModelError("", errorMessage);

                    if (!string.IsNullOrEmpty(result.RejectionReason))
                    {
                        ModelState.AddModelError("", $"Rejection Reason: {result.RejectionReason}");
                    }

                    // Restore model data for the view
                    model.IdDocumentType = documentData.IdDocumentType;
                    model.ExtractedIdNumber = documentData.ExtractedIdNumber;
                    model.ExtractedIdName = documentData.ExtractedIdName;
                    model.IdDocumentExpiryDate = documentData.IdDocumentExpiryDate;
                    model.IdDocumentHasNoExpiration = documentData.IdDocumentHasNoExpiration;

                    return View(model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during face verification for user {UserId}", GetCurrentUserId());
                ModelState.AddModelError("", "An error occurred during face verification. Please try again.");
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Status()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return RedirectToAction("Login", "Account");
                }

                var status = await _verificationService.GetVerificationStatusAsync(userId);
                return View(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving verification status");
                return View("Error");
            }
        }

        [HttpGet]
        public async Task<IActionResult> CheckStatus()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }

                var isVerified = await _verificationService.IsUserVerifiedAsync(userId);
                var canPostProject = await _verificationService.CanUserPostProjectAsync(userId);
                var canBid = await _verificationService.CanUserBidAsync(userId);

                return Json(new { success = true, isVerified, canPostProject, canBid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking verification status");
                return Json(new { success = false, message = "Error checking status" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CaptureLiveFace([FromBody] LiveFaceCaptureViewModel model)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }

                return Json(new { success = true, message = "Live face capture ready" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in live face capture");
                return Json(new { success = false, message = "Error in live face capture" });
            }
        }

        private string GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return string.IsNullOrEmpty(userIdClaim) ? string.Empty : userIdClaim;
        }
    }

    public class DocumentVerificationData
    {
        [JsonPropertyName("idDocumentType")]
        public string IdDocumentType { get; set; }

        [JsonPropertyName("extractedIdNumber")]
        public string? ExtractedIdNumber { get; set; }

        [JsonPropertyName("idDocumentExpiryDate")]
        public DateTime? IdDocumentExpiryDate { get; set; }

        [JsonPropertyName("idDocumentHasNoExpiration")]
        public bool IdDocumentHasNoExpiration { get; set; }

        [JsonPropertyName("idDocumentVerified")]
        public bool IdDocumentVerified { get; set; }

        [JsonPropertyName("idDocumentConfidence")]
        public float IdDocumentConfidence { get; set; }

        [JsonPropertyName("idDocumentMessage")]
        public string IdDocumentMessage { get; set; }

        [JsonPropertyName("idDocumentImageData")]
        public string? IdDocumentImageData { get; set; } // Now stores Base64 data instead of URL

        [JsonPropertyName("idDocumentImageContentType")]
        public string? IdDocumentImageContentType { get; set; }

        [JsonPropertyName("extractedIdName")]
        public string? ExtractedIdName { get; set; }
    }
}