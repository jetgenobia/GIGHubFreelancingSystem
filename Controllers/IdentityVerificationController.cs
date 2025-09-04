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

        public IdentityVerificationController(
            IIdentityVerificationService verificationService,
            ILogger<IdentityVerificationController> logger)
        {
            _verificationService = verificationService;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
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

                    // Always set ViewBag data if image exists in session
                    if (!string.IsNullOrEmpty(documentData.IdDocumentImageData))
                    {
                        ViewBag.StoredImageData = documentData.IdDocumentImageData;
                        ViewBag.StoredImageContentType = documentData.IdDocumentImageContentType;
                        ViewBag.HasStoredImage = true;

                        _logger.LogInformation(
                            "Document GET: Restored session data for user. Has extracted ID: {HasExtractedId}, Image data length: {ImageLength}",
                            !string.IsNullOrEmpty(documentData.ExtractedIdNumber),
                            documentData.IdDocumentImageData.Length);
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
                            // Process new image
                            using (var memoryStream = new MemoryStream())
                            {
                                await model.IdDocumentImage.CopyToAsync(memoryStream);
                                var imageBytes = memoryStream.ToArray();
                                documentData.IdDocumentImageData = Convert.ToBase64String(imageBytes);
                                documentData.IdDocumentImageContentType = model.IdDocumentImage.ContentType;
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

                    // Process uploaded document
                    using (var memoryStream = new MemoryStream())
                    {
                        await model.IdDocumentImage.CopyToAsync(memoryStream);
                        var imageBytes = memoryStream.ToArray();
                        documentData.IdDocumentImageData = Convert.ToBase64String(imageBytes);
                        documentData.IdDocumentImageContentType = model.IdDocumentImage.ContentType;
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
            TempData["ErrorMessage"] = "No document data found. Please complete the document verification step first.";
            return RedirectToAction("Document");
        }

        [HttpPost]
        public async Task<IActionResult> Verify(FaceVerificationViewModel model)
        {
            _logger.LogInformation("Verify POST action called");
            _logger.LogInformation("LiveFaceImageData length: {Length}",
                model.LiveFaceImageData?.Length ?? 0);
            _logger.LogInformation("AgreeToTerms: {AgreeToTerms}", model.AgreeToTerms);

            _logger.LogInformation("=== VERIFY POST ACTION HIT ===");
            _logger.LogInformation("Request method: {Method}", Request.Method);
            _logger.LogInformation("Content type: {ContentType}", Request.ContentType);

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
                    documentData.IdDocumentImageData // <-- pass the session-stored base64 image here
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
        public string? IdDocumentImageData { get; set; }

        [JsonPropertyName("idDocumentImageContentType")]
        public string? IdDocumentImageContentType { get; set; }

        [JsonPropertyName("extractedIdName")]
        public string? ExtractedIdName { get; set; }
    }
}