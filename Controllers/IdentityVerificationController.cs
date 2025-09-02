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
            // Check if there's stored document data (when coming back from face verification)
            var sessionDocumentData = HttpContext.Session.GetString("DocumentData");
            
            if (!string.IsNullOrEmpty(sessionDocumentData))
            {
                try
                {
                    var documentData = System.Text.Json.JsonSerializer.Deserialize<DocumentVerificationData>(sessionDocumentData);
                    
                    // Create view model with stored data
                    var model = new DocumentVerificationViewModel
                    {
                        IdDocumentType = documentData.IdDocumentType,
                        IdDocumentNumber = documentData.IdDocumentNumber,
                        IdDocumentExpiryDate = documentData.IdDocumentExpiryDate,
                        IdDocumentHasNoExpiration = documentData.IdDocumentHasNoExpiration
                    };
                    
                    // Store the image data in ViewBag for the view to access
                    if (!string.IsNullOrEmpty(documentData.IdDocumentImageData))
                    {
                        ViewBag.StoredImageData = documentData.IdDocumentImageData;
                        ViewBag.StoredImageContentType = documentData.IdDocumentImageContentType;
                        _logger.LogInformation("ViewBag image data set for Document GET. Image data length: {Length}, Content type: {ContentType}", 
                            documentData.IdDocumentImageData.Length, documentData.IdDocumentImageContentType);
                    }
                    else
                    {
                        _logger.LogInformation("No image data found in session for Document GET");
                    }
                    
                    return View(model);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deserializing document data from session in Document GET");
                    // If deserialization fails, clear the corrupted session data and return empty model
                    HttpContext.Session.Remove("DocumentData");
                    return View(new DocumentVerificationViewModel());
                }
            }
            
            return View(new DocumentVerificationViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Document(DocumentVerificationViewModel model)
        {
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

                // Handle expiry date logic
                DateTime? expiryDate = null;
                if (!model.IdDocumentHasNoExpiration && model.IdDocumentExpiryDate.HasValue)
                {
                    expiryDate = model.IdDocumentExpiryDate.Value;
                }

                // Check if this is a returning user with existing session data
                var existingSessionData = HttpContext.Session.GetString("DocumentData");
                if (!string.IsNullOrEmpty(existingSessionData))
                {
                    try
                    {
                        var existingData = System.Text.Json.JsonSerializer.Deserialize<DocumentVerificationData>(existingSessionData);

                        // Check if user made any changes to the form
                        bool hasChanges = existingData.IdDocumentType != model.IdDocumentType ||
                                        existingData.IdDocumentNumber != model.IdDocumentNumber ||
                                        existingData.IdDocumentExpiryDate != expiryDate ||
                                        existingData.IdDocumentHasNoExpiration != model.IdDocumentHasNoExpiration ||
                                        (model.IdDocumentImage != null && model.IdDocumentImage.Length > 0);

                        if (hasChanges)
                        {
                            // User made changes to form fields, update the session data while preserving image data
                            existingData.IdDocumentType = model.IdDocumentType;
                            existingData.IdDocumentNumber = model.IdDocumentNumber;
                            existingData.IdDocumentExpiryDate = expiryDate;
                            existingData.IdDocumentHasNoExpiration = model.IdDocumentHasNoExpiration;
                            existingData.IdDocumentMessage = "Document data updated, pending verification";
                            // Note: existingData.IdDocumentImageData and IdDocumentImageContentType are preserved from existing session

                            // Save updated data back to session
                            var updatedSerializedData = System.Text.Json.JsonSerializer.Serialize(existingData);
                            HttpContext.Session.SetString("DocumentData", updatedSerializedData);

                            // Log for debugging
                            _logger.LogInformation("Updated document data for user {UserId}. Image data preserved: {HasImageData}",
                                userId, !string.IsNullOrEmpty(existingData.IdDocumentImageData));
                        }
                        else
                        {
                            // No changes detected, reuse existing data
                            HttpContext.Session.SetString("DocumentData", existingSessionData);
                        }

                        // Redirect to face verification
                        return RedirectToAction("Verify");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error updating document data for returning user");
                        // Fall through to create new document data if deserialization fails
                    }
                }

                // Create new document data
                var documentData = new DocumentVerificationData
                {
                    IdDocumentType = model.IdDocumentType,
                    IdDocumentNumber = model.IdDocumentNumber,
                    IdDocumentExpiryDate = expiryDate,
                    IdDocumentHasNoExpiration = model.IdDocumentHasNoExpiration,
                    IdDocumentVerified = false, // Will be set to true after API verification
                    IdDocumentConfidence = 0.0f, // Will be set after API verification
                    IdDocumentMessage = "Pending verification", // Will be updated after API verification
                    IdDocumentImageData = null, // Will be set if image is uploaded
                    IdDocumentImageContentType = null // Will be set if image is uploaded
                };

                // If an image was uploaded, convert it to base64 and store it
                if (model.IdDocumentImage != null && model.IdDocumentImage.Length > 0)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        await model.IdDocumentImage.CopyToAsync(memoryStream);
                        var imageBytes = memoryStream.ToArray();
                        var base64String = Convert.ToBase64String(imageBytes);

                        documentData.IdDocumentImageData = base64String;
                        documentData.IdDocumentImageContentType = model.IdDocumentImage.ContentType;
                    }

                    // *** NEW: Call Google Vision API immediately to extract ID number and name ***
                    try
                    {
                        var documentResult = await _verificationService.VerifyIdDocumentAsync(
                            model.IdDocumentImage,
                            model.IdDocumentType,
                            model.IdDocumentNumber,
                            expiryDate,
                            model.IdDocumentHasNoExpiration,
                            userId
                        );

                        // Store the extracted data
                        documentData.ExtractedIdName = documentResult.extractedIdName;
                        documentData.ExtractedIdNumber = documentResult.extractedIdNumber;
                        documentData.IdDocumentVerified = documentResult.verified;
                        documentData.IdDocumentConfidence = documentResult.confidence;
                        documentData.IdDocumentMessage = documentResult.message;

                        _logger.LogInformation("Document verification completed for user {UserId}. Extracted name: {ExtractedName}, Extracted ID: {ExtractedId}",
                            userId, documentResult.extractedIdName, documentResult.extractedIdNumber);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during document verification for user {UserId}", userId);
                        // Continue with the flow even if extraction fails - user can still proceed
                        documentData.IdDocumentMessage = "Document uploaded but extraction failed. Please verify manually.";
                    }
                }

                // Store document data in Session for the next step
                var serializedData = System.Text.Json.JsonSerializer.Serialize(documentData);
                HttpContext.Session.SetString("DocumentData", serializedData);

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
            // Retrieve document data from Session if available
            var sessionDocumentData = HttpContext.Session.GetString("DocumentData");

            if (!string.IsNullOrEmpty(sessionDocumentData))
            {
                try
                {
                    var documentData = System.Text.Json.JsonSerializer.Deserialize<DocumentVerificationData>(sessionDocumentData);

                    // Create a new view model with the stored data including extracted information
                    var model = new FaceVerificationViewModel
                    {
                        IdDocumentType = documentData.IdDocumentType,
                        IdDocumentNumber = documentData.IdDocumentNumber,
                        IdDocumentExpiryDate = documentData.IdDocumentExpiryDate,
                        IdDocumentHasNoExpiration = documentData.IdDocumentHasNoExpiration,
                        ExtractedIdName = documentData.ExtractedIdName,
                        ExtractedIdNumber = documentData.ExtractedIdNumber
                    };

                    return View(model);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deserializing document data from Session");
                    // If deserialization fails, redirect to document step
                    return RedirectToAction("Document");
                }
            }

            return View(new FaceVerificationViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Verify(FaceVerificationViewModel model)
        {
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

                // Get the stored document data from Session
                var sessionDocumentData = HttpContext.Session.GetString("DocumentData");
                if (string.IsNullOrEmpty(sessionDocumentData))
                {
                    ModelState.AddModelError("", "Document data not found. Please start over from the document verification step.");
                    return RedirectToAction("Document");
                }

                var documentData = System.Text.Json.JsonSerializer.Deserialize<DocumentVerificationData>(sessionDocumentData);

                // Convert stored image data to bytes for API call
                byte[]? storedImageBytes = null;
                if (!string.IsNullOrEmpty(documentData.IdDocumentImageData))
                {
                    storedImageBytes = Convert.FromBase64String(documentData.IdDocumentImageData);
                }

                // Now call the Google Cloud API for document verification using stored data
                var documentResult = await _verificationService.VerifyIdDocumentAsync(
                    null, // No new file uploaded
                    documentData.IdDocumentType,
                    documentData.IdDocumentNumber,
                    documentData.IdDocumentExpiryDate,
                    documentData.IdDocumentHasNoExpiration,
                    userId,
                    storedImageBytes // Pass the stored image bytes
                );

                documentData.ExtractedIdName = documentResult.extractedIdName;
                documentData.ExtractedIdNumber = documentResult.extractedIdNumber;

                var updatedSerializedData = System.Text.Json.JsonSerializer.Serialize(documentData);
                HttpContext.Session.SetString("DocumentData", updatedSerializedData);

                // Create face model with extracted details
                var faceModel = new FaceVerificationViewModel
                {
                    IdDocumentType = documentData.IdDocumentType,
                    IdDocumentNumber = documentData.IdDocumentNumber,
                    IdDocumentExpiryDate = documentData.IdDocumentExpiryDate,
                    IdDocumentHasNoExpiration = documentData.IdDocumentHasNoExpiration,
                    ExtractedIdName = documentData.ExtractedIdName,
                    ExtractedIdNumber = documentData.ExtractedIdNumber
                };

                // Only proceed with face verification if document verification passed
                if (!documentResult.Item1)
                {
                    ModelState.AddModelError("", $"Document verification failed: {documentResult.Item2}");
                    return RedirectToAction("Document");
                }

                // Now verify the live face using Google Vision API
                var faceResult = await _verificationService.VerifyLiveFaceAsync(model.LiveFaceImageData, userId);

                if (!faceResult.Item1) // If face verification failed
                {
                    ModelState.AddModelError("", $"Face verification failed: {faceResult.Item2}");
                    return View(faceModel);
                }

                // Both verifications passed, save the complete verification data
                var result = await _verificationService.CompleteVerificationAsync(
                    model.LiveFaceImageData, 
                    userId, 
                    documentData.IdDocumentType,
                    documentData.IdDocumentNumber,
                    documentData.IdDocumentExpiryDate,
                    documentData.IdDocumentHasNoExpiration,
                    documentResult.Item1, // idDocumentVerified from the API call
                    documentResult.Item3  // idDocumentConfidence from the API call
                );

                if (result.Success)
                {
                    TempData["SuccessMessage"] = "Identity verification completed successfully!";
                    return RedirectToAction("Status");
                }
                else
                {
                    var errorMessage = !string.IsNullOrEmpty(result.Message) ? result.Message : "Verification failed. Please try again.";
                    ModelState.AddModelError("", errorMessage);
                    if (!string.IsNullOrEmpty(result.RejectionReason))
                    {
                        ModelState.AddModelError("", $"Rejection Reason: {result.RejectionReason}");
                    }
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during face verification");
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

                return Json(new
                {
                    success = true,
                    isVerified,
                    canPostProject,
                    canBid
                });
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

                // This endpoint can be used for AJAX calls to capture live face
                // The actual verification will happen in the main Verify action
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
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return string.Empty;
            }
            return userIdClaim;
        }
    }

    // Data transfer object for storing document verification data in session
    public class DocumentVerificationData
    {
        [JsonPropertyName("idDocumentType")]
        public string IdDocumentType { get; set; }
        
        [JsonPropertyName("idDocumentNumber")]
        public string IdDocumentNumber { get; set; }
        
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
        public string? IdDocumentImageData { get; set; } // Base64 encoded image data
        
        [JsonPropertyName("idDocumentImageContentType")]
        public string? IdDocumentImageContentType { get; set; } // Image MIME type
        [JsonPropertyName("extractedIdName")]
        public string? ExtractedIdName { get; set; }

        [JsonPropertyName("extractedIdNumber")]
        public string? ExtractedIdNumber { get; set; }
    }
}
