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

                    // NEW: Set original ID type for change tracking
                    model.OriginalIdType = documentData.IdDocumentType;

                    // Rest of your existing code remains the same...
                    model.ExtractedIdNumber = documentData.ExtractedIdNumber == "OCR_FAILED" ? null : documentData.ExtractedIdNumber;
                    model.ExtractedIdName = documentData.ExtractedIdName == "OCR_FAILED" ? null : documentData.ExtractedIdName;
                    model.IdDocumentExpiryDate = documentData.IdDocumentExpiryDate;
                    model.IdDocumentHasNoExpiration = documentData.IdDocumentHasNoExpiration;
                    model.ExtractedExpiryDate = documentData.ExtractedExpiryDate;

                    // Keep all your existing ViewBag logic...
                    if (!string.IsNullOrEmpty(documentData.IdDocumentImageData))
                    {
                        ViewBag.StoredImageData = documentData.IdDocumentImageData;
                        ViewBag.StoredImageContentType = documentData.IdDocumentImageContentType;
                        ViewBag.HasStoredImage = true;

                        _logger.LogInformation(
                            "Document GET: Restored session data for user. Has extracted ID: {HasExtractedId}, Image data length: {ImageLength}",
                            !string.IsNullOrEmpty(documentData.ExtractedIdNumber) && documentData.ExtractedIdNumber != "OCR_FAILED",
                            documentData.IdDocumentImageData.Length);
                    }
                    else
                    {
                        ViewBag.HasStoredImage = false;
                        _logger.LogInformation("Document GET: No image data found in session");
                    }

                    ViewBag.ExtractedIdName = documentData.ExtractedIdName == "OCR_FAILED" ? null : documentData.ExtractedIdName;
                    ViewBag.HasExtractedData = !string.IsNullOrEmpty(documentData.ExtractedIdNumber) && documentData.ExtractedIdNumber != "OCR_FAILED";
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
            // ===== ENHANCED DEBUGGING - START =====
            var userId = GetCurrentUserId();
            _logger.LogCritical("=== DOCUMENT POST DEBUG START === User: {UserId}", userId);
            _logger.LogCritical("Form received - IdDocumentType: {IdType}", model.IdDocumentType);
            _logger.LogCritical("Original ID Type: {OriginalType}", model.OriginalIdType);

            // NEW: Detect ID type changes
            bool hasIdTypeChanged = !string.IsNullOrEmpty(model.OriginalIdType) &&
                                   model.OriginalIdType != model.IdDocumentType;

            _logger.LogCritical("ID Type Changed: {Changed} (from '{Original}' to '{New}')",
                hasIdTypeChanged, model.OriginalIdType, model.IdDocumentType);

            // NEW: If ID type changed, clear session data to treat as fresh form
            if (hasIdTypeChanged)
            {
                _logger.LogCritical("🔄 ID TYPE CHANGED - CLEARING SESSION DATA TO TREAT AS FRESH FORM");
                HttpContext.Session.Remove("DocumentData");

                // Reset model to fresh state
                model.ExtractedIdNumber = null;
                model.ExtractedIdName = null;
                model.ExtractedExpiryDate = null;

                _logger.LogCritical("✅ Session cleared and form reset to fresh state");
            }

            // Debug all form fields
            _logger.LogCritical("=== FORM DATA DEBUG ===");
            foreach (var key in Request.Form.Keys)
            {
                var values = Request.Form[key];
                if (key == "DocumentImageData")
                {
                    _logger.LogCritical("Form Key: {Key}, Value Length: {Length} chars", key, values.ToString().Length);
                    _logger.LogCritical("DocumentImageData starts with: {Start}",
                        values.ToString().Length > 50 ? values.ToString().Substring(0, 50) : values.ToString());
                }
                else
                {
                    _logger.LogCritical("Form Key: {Key}, Value: {Value}", key, values);
                }
            }

            // Check for camera-captured image data with detailed logging
            string cameraImageData = Request.Form["DocumentImageData"];
            bool hasCameraData = !string.IsNullOrEmpty(cameraImageData) && cameraImageData.Length > 1000;

            _logger.LogCritical("=== CAMERA DATA ANALYSIS ===");
            _logger.LogCritical("Camera data exists: {Exists}", !string.IsNullOrEmpty(cameraImageData));
            _logger.LogCritical("Camera data length: {Length}", cameraImageData?.Length ?? 0);
            _logger.LogCritical("Camera data valid (>1000 chars): {Valid}", hasCameraData);

            if (!string.IsNullOrEmpty(cameraImageData))
            {
                _logger.LogCritical("Camera data first 100 chars: {Preview}",
                    cameraImageData.Length > 100 ? cameraImageData.Substring(0, 100) : cameraImageData);
            }
            // ===== ENHANCED DEBUGGING - END =====

            // Restore ViewBag data for validation failures (only if ID type hasn't changed)
            var sessionData = HttpContext.Session.GetString("DocumentData");
            DocumentVerificationData existingDocumentData = null;

            if (!hasIdTypeChanged && !string.IsNullOrEmpty(sessionData))
            {
                try
                {
                    existingDocumentData = System.Text.Json.JsonSerializer.Deserialize<DocumentVerificationData>(sessionData);

                    // Restore model data that might be missing from form submission
                    if (string.IsNullOrEmpty(model.ExtractedIdNumber) && !string.IsNullOrEmpty(existingDocumentData.ExtractedIdNumber))
                    {
                        model.ExtractedIdNumber = existingDocumentData.ExtractedIdNumber;
                    }

                    if (string.IsNullOrEmpty(model.ExtractedIdName) && !string.IsNullOrEmpty(existingDocumentData.ExtractedIdName))
                    {
                        model.ExtractedIdName = existingDocumentData.ExtractedIdName;
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
            else if (hasIdTypeChanged)
            {
                // Clear ViewBag data for fresh form state when ID type changed
                ViewBag.HasStoredImage = false;
                ViewBag.HasExtractedData = false;
                ViewBag.StoredImageData = null;
                ViewBag.StoredImageContentType = null;
                ViewBag.ExtractedIdName = null;
            }

            // NEW: Enhanced validation logic that treats ID type changes as fresh forms
            bool isReturningUser = !hasIdTypeChanged && // KEY CHANGE: Not returning user if ID type changed
                                  (!string.IsNullOrEmpty(model.ExtractedIdNumber) ||
                                   (existingDocumentData != null && !string.IsNullOrEmpty(existingDocumentData.ExtractedIdNumber)));

            bool hasStoredImage = !hasIdTypeChanged && // KEY CHANGE: No stored image if ID type changed
                                 existingDocumentData != null && !string.IsNullOrEmpty(existingDocumentData.IdDocumentImageData);

            bool hasNewUpload = model.IdDocumentImage != null && model.IdDocumentImage.Length > 0;

            _logger.LogCritical("=== VALIDATION CONDITIONS ===");
            _logger.LogCritical("Has ID type changed: {HasChanged}", hasIdTypeChanged);
            _logger.LogCritical("Is returning user: {IsReturning}", isReturningUser);
            _logger.LogCritical("Has stored image: {HasStored}", hasStoredImage);
            _logger.LogCritical("Has new upload: {HasUpload}", hasNewUpload);
            _logger.LogCritical("Has camera data: {HasCamera}", hasCameraData);

            // FIXED: Only add validation error if there's no new upload AND no camera data
            if (!isReturningUser && (!hasStoredImage && !hasNewUpload && !hasCameraData))
            {
                if (hasIdTypeChanged)
                {
                    _logger.LogCritical("=== VALIDATION FAILED - ID TYPE CHANGED, NO NEW IMAGE DATA ===");
                    ModelState.AddModelError("IdDocumentImage", $"ID type changed to {model.IdDocumentType}. Please upload your {model.IdDocumentType} document or capture it using the camera.");
                }
                else
                {
                    _logger.LogCritical("=== VALIDATION FAILED - NEW USER NEEDS IMAGE ===");
                    ModelState.AddModelError("IdDocumentImage", "Please upload your ID document or capture it using the camera.");
                }
            }
            else if (hasIdTypeChanged && (hasNewUpload || hasCameraData))
            {
                _logger.LogCritical("=== ID TYPE CHANGED WITH NEW IMAGE - VALIDATION PASSED ===");
            }

            if (!ModelState.IsValid)
            {
                // Preserve camera data when validation fails
                if (hasCameraData && string.IsNullOrEmpty(sessionData))
                {
                    try
                    {
                        var tempDocumentData = new DocumentVerificationData
                        {
                            IdDocumentType = model.IdDocumentType,
                            IdDocumentImageData = cameraImageData.Contains(',') ? cameraImageData.Split(',')[1] : cameraImageData,
                            IdDocumentImageContentType = "image/jpeg",
                            ExtractedIdNumber = null,
                            ExtractedIdName = null,
                            IdDocumentHasNoExpiration = model.IdDocumentHasNoExpiration,
                            IdDocumentMessage = hasIdTypeChanged ?
                                $"Image captured for {model.IdDocumentType}, awaiting processing" :
                                "Image captured, awaiting processing"
                        };

                        HttpContext.Session.SetString("DocumentData", System.Text.Json.JsonSerializer.Serialize(tempDocumentData));

                        // Set ViewBag for immediate display
                        ViewBag.StoredImageData = tempDocumentData.IdDocumentImageData;
                        ViewBag.StoredImageContentType = "image/jpeg";
                        ViewBag.HasStoredImage = true;
                        ViewBag.HasExtractedData = false;

                        _logger.LogInformation("Preserved camera data in session during validation failure for user {UserId}", userId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to preserve camera data during validation failure");
                    }
                }

                _logger.LogCritical("=== MODEL STATE INVALID - RETURNING VIEW ===");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    _logger.LogCritical("ModelState Error: {Error}", error.ErrorMessage);
                }
                return View(model);
            }

            _logger.LogCritical("=== VALIDATION PASSED - CONTINUING PROCESSING ===");

            // Continue with processing logic...
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    return RedirectToAction("Login", "Account");
                }

                // Handle automatic expiration date logic based on document type
                DateTime? expiryDate = null;
                bool hasNoExpiration = false;

                if (string.Equals(model.IdDocumentType, "National ID", StringComparison.OrdinalIgnoreCase))
                {
                    expiryDate = null;
                    hasNoExpiration = true;
                }
                else
                {
                    if (model.ExtractedExpiryDate.HasValue)
                    {
                        expiryDate = model.ExtractedExpiryDate.Value;
                        hasNoExpiration = false;
                    }
                    else if (!model.IdDocumentHasNoExpiration && model.IdDocumentExpiryDate.HasValue)
                    {
                        expiryDate = model.IdDocumentExpiryDate.Value;
                        hasNoExpiration = false;
                    }
                    else if (model.IdDocumentHasNoExpiration)
                    {
                        expiryDate = null;
                        hasNoExpiration = true;
                    }
                }

                DocumentVerificationData documentData;

                // Since we treat ID type changes as fresh, we'll either process existing data or create new
                if (!hasIdTypeChanged && existingDocumentData != null)
                {
                    // Process existing document data (no ID type change)
                    documentData = existingDocumentData;

                    bool hasFormChanges =
                        documentData.IdDocumentType != model.IdDocumentType ||
                        documentData.IdDocumentExpiryDate != expiryDate ||
                        documentData.IdDocumentHasNoExpiration != hasNoExpiration;

                    _logger.LogCritical("Processing existing data - Has form changes: {HasFormChanges}", hasFormChanges);

                    if (hasFormChanges || hasNewUpload || hasCameraData)
                    {
                        // Update form data
                        documentData.IdDocumentType = model.IdDocumentType;
                        documentData.IdDocumentExpiryDate = expiryDate;
                        documentData.IdDocumentHasNoExpiration = hasNoExpiration;

                        if (string.Equals(model.IdDocumentType, "National ID", StringComparison.OrdinalIgnoreCase))
                        {
                            documentData.ExtractedExpiryDate = null;
                        }

                        // Handle new image uploads or camera captures
                        if (hasNewUpload || hasCameraData)
                        {
                            // Handle image data (file upload or camera capture)
                            string imageData = null;
                            string contentType = null;

                            if (hasNewUpload)
                            {
                                // Use uploaded file
                                using (var memoryStream = new MemoryStream())
                                {
                                    await model.IdDocumentImage.CopyToAsync(memoryStream);
                                    var imageBytes = memoryStream.ToArray();
                                    imageData = Convert.ToBase64String(imageBytes);
                                    contentType = model.IdDocumentImage.ContentType;
                                }
                            }
                            else if (hasCameraData)
                            {
                                try
                                {
                                    // Extract base64 data from data URL and convert to bytes
                                    var base64Data = cameraImageData.Split(',')[1];
                                    imageData = base64Data;
                                    contentType = "image/jpeg";

                                    _logger.LogInformation("Camera data processed successfully, image size: {Size} bytes",
                                        Convert.FromBase64String(base64Data).Length);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error processing camera-captured image data");

                                    if (!string.IsNullOrEmpty(cameraImageData))
                                    {
                                        try
                                        {
                                            imageData = cameraImageData.Split(',')[1];
                                            contentType = "image/jpeg";
                                            _logger.LogWarning("Using raw camera data after processing failed");
                                        }
                                        catch (Exception innerEx)
                                        {
                                            _logger.LogError(innerEx, "Failed to extract base64 data from camera input");
                                            ModelState.AddModelError("", "Error processing captured image. Please try again.");
                                            return View(model);
                                        }
                                    }
                                    else
                                    {
                                        ModelState.AddModelError("", "Error processing captured image. Please try again.");
                                        return View(model);
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(imageData))
                            {
                                documentData.IdDocumentImageData = imageData;
                                documentData.IdDocumentImageContentType = contentType;

                                try
                                {
                                    var documentResult = await _verificationService.VerifyIdDocumentAsync(
                                        null, // No IFormFile
                                        model.IdDocumentType,
                                        null,
                                        expiryDate,
                                        model.IdDocumentHasNoExpiration,
                                        userId,
                                        Convert.FromBase64String(imageData)
                                    );

                                    documentData.ExtractedIdName = documentResult.extractedIdName;
                                    documentData.ExtractedIdNumber = documentResult.extractedIdNumber;
                                    documentData.IdDocumentVerified = documentResult.verified;
                                    documentData.IdDocumentConfidence = documentResult.confidence;
                                    documentData.IdDocumentMessage = documentResult.message;

                                    var validationMessage = GetDocumentValidationMessage(
                                        model.IdDocumentType,
                                        documentResult.extractedIdNumber,
                                        documentResult.extractedIdName,
                                        documentResult.message
                                    );

                                    if (!string.Equals(validationMessage, documentResult.message, StringComparison.Ordinal))
                                    {
                                        ViewBag.ValidationWarning = validationMessage;
                                        documentData.IdDocumentMessage = validationMessage;

                                        if (validationMessage.Contains("Document type mismatch"))
                                        {
                                            documentData.IdDocumentVerified = false;
                                            documentData.IdDocumentConfidence = 0.0f;
                                        }
                                    }
                                    else if (!documentResult.verified || string.IsNullOrEmpty(documentResult.extractedIdNumber))
                                    {
                                        ViewBag.ValidationWarning = validationMessage;
                                        documentData.IdDocumentMessage = validationMessage;
                                    }

                                    if (documentResult.extractedExpiryDate.HasValue)
                                    {
                                        documentData.ExtractedExpiryDate = documentResult.extractedExpiryDate;
                                        if (!string.Equals(model.IdDocumentType, "National ID", StringComparison.OrdinalIgnoreCase))
                                        {
                                            documentData.IdDocumentExpiryDate = documentResult.extractedExpiryDate;
                                            documentData.IdDocumentHasNoExpiration = false;
                                        }
                                    }

                                    _logger.LogInformation(
                                        "Document verification completed for user {UserId}. Source: {Source}, Extracted name: {ExtractedName}, Extracted ID: {ExtractedId}",
                                        userId, hasCameraData ? "Camera" : "Upload", documentResult.extractedIdName, documentResult.extractedIdNumber);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error during document verification for user {UserId}", userId);

                                    documentData.ExtractedIdName = "OCR_FAILED";
                                    documentData.ExtractedIdNumber = "OCR_FAILED";
                                    documentData.IdDocumentMessage = "⚠️ We couldn't process your document automatically. This may be due to image quality, lighting, or document clarity. Please try uploading a clearer image or contact support for manual verification.";
                                    documentData.IdDocumentVerified = false;
                                    documentData.IdDocumentConfidence = 0.0f;

                                    ViewBag.ProcessingError = documentData.IdDocumentMessage;
                                    _logger.LogWarning("Stored image with OCR failure placeholders for user {UserId}", userId);
                                }

                                HttpContext.Session.SetString("DocumentData", System.Text.Json.JsonSerializer.Serialize(documentData));
                                _logger.LogInformation("Image stored in session for user {UserId}, image length: {Length}", userId, imageData.Length);
                            }
                        }
                        else
                        {
                            if (string.Equals(model.IdDocumentType, "National ID", StringComparison.OrdinalIgnoreCase))
                            {
                                documentData.ExtractedExpiryDate = null;
                            }
                            documentData.IdDocumentMessage = "Document data updated, using existing document image";
                        }

                        HttpContext.Session.SetString("DocumentData", System.Text.Json.JsonSerializer.Serialize(documentData));
                    }

                    if (!string.IsNullOrEmpty(ViewBag.ValidationWarning as string))
                    {
                        // Restore form data for user
                        ViewBag.StoredImageData = documentData.IdDocumentImageData;
                        ViewBag.StoredImageContentType = documentData.IdDocumentImageContentType;
                        ViewBag.HasStoredImage = true;
                        ViewBag.ExtractedIdName = documentData.ExtractedIdName;
                        ViewBag.HasExtractedData = !string.IsNullOrEmpty(documentData.ExtractedIdNumber);

                        model.ExtractedIdNumber = documentData.ExtractedIdNumber;
                        model.ExtractedIdName = documentData.ExtractedIdName;
                        model.ExtractedExpiryDate = documentData.ExtractedExpiryDate;

                        return View(model);
                    }

                    return RedirectToAction("Verify");
                }
                else
                {
                    // Create new document data (either fresh form or ID type changed)
                    _logger.LogCritical("Creating new document data - Fresh form or ID type changed");

                    documentData = new DocumentVerificationData
                    {
                        IdDocumentType = model.IdDocumentType,
                        ExtractedIdNumber = null,
                        IdDocumentExpiryDate = expiryDate,
                        IdDocumentHasNoExpiration = hasNoExpiration,
                        IdDocumentVerified = false,
                        IdDocumentConfidence = 0.0f,
                        IdDocumentMessage = hasIdTypeChanged ?
                            $"Processing {model.IdDocumentType} document (ID type changed)" : "Pending verification",
                        IdDocumentImageData = null,
                        IdDocumentImageContentType = null
                    };

                    // Handle image data (file upload or camera capture)
                    string imageData = null;
                    string contentType = null;

                    if (hasNewUpload)
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            await model.IdDocumentImage.CopyToAsync(memoryStream);
                            var imageBytes = memoryStream.ToArray();
                            imageData = Convert.ToBase64String(imageBytes);
                            contentType = model.IdDocumentImage.ContentType;
                        }
                    }
                    else if (hasCameraData)
                    {
                        try
                        {
                            var base64Data = cameraImageData.Split(',')[1];
                            imageData = base64Data;
                            contentType = "image/jpeg";

                            _logger.LogInformation("Camera data processed successfully for new user, image size: {Size} bytes",
                                Convert.FromBase64String(base64Data).Length);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing camera-captured image data for new user");

                            if (!string.IsNullOrEmpty(cameraImageData))
                            {
                                try
                                {
                                    imageData = cameraImageData.Split(',')[1];
                                    contentType = "image/jpeg";
                                    _logger.LogWarning("Using raw camera data after processing failed for new user");
                                }
                                catch (Exception innerEx)
                                {
                                    _logger.LogError(innerEx, "Failed to extract base64 data from camera input for new user");
                                    ModelState.AddModelError("", "Error processing captured image. Please try again.");
                                    return View(model);
                                }
                            }
                            else
                            {
                                ModelState.AddModelError("", "Error processing captured image. Please try again.");
                                return View(model);
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(imageData))
                    {
                        documentData.IdDocumentImageData = imageData;
                        documentData.IdDocumentImageContentType = contentType;

                        try
                        {
                            var documentResult = await _verificationService.VerifyIdDocumentAsync(
                                null, // No IFormFile
                                model.IdDocumentType,
                                null,
                                expiryDate,
                                model.IdDocumentHasNoExpiration,
                                userId,
                                Convert.FromBase64String(imageData)
                            );

                            documentData.ExtractedIdName = documentResult.extractedIdName;
                            documentData.ExtractedIdNumber = documentResult.extractedIdNumber;
                            documentData.IdDocumentVerified = documentResult.verified;
                            documentData.IdDocumentConfidence = documentResult.confidence;
                            documentData.IdDocumentMessage = documentResult.message;

                            var validationMessage = GetDocumentValidationMessage(
                                model.IdDocumentType,
                                documentResult.extractedIdNumber,
                                documentResult.extractedIdName,
                                documentResult.message
                            );

                            if (!string.Equals(validationMessage, documentResult.message, StringComparison.Ordinal))
                            {
                                ViewBag.ValidationWarning = validationMessage;
                                documentData.IdDocumentMessage = validationMessage;

                                if (validationMessage.Contains("Document type mismatch"))
                                {
                                    documentData.IdDocumentVerified = false;
                                    documentData.IdDocumentConfidence = 0.0f;
                                }
                            }
                            else if (!documentResult.verified || string.IsNullOrEmpty(documentResult.extractedIdNumber))
                            {
                                ViewBag.ValidationWarning = validationMessage;
                                documentData.IdDocumentMessage = validationMessage;
                            }

                            if (documentResult.extractedExpiryDate.HasValue)
                            {
                                documentData.ExtractedExpiryDate = documentResult.extractedExpiryDate;
                                if (!string.Equals(model.IdDocumentType, "National ID", StringComparison.OrdinalIgnoreCase))
                                {
                                    documentData.IdDocumentExpiryDate = documentResult.extractedExpiryDate;
                                    documentData.IdDocumentHasNoExpiration = false;
                                }
                            }

                            if (hasIdTypeChanged)
                            {
                                /*ViewBag.Message = $"✅ Document processed successfully with {model.IdDocumentType} settings!";*/
                                _logger.LogInformation("Document processed after ID type change for user {UserId} to {NewType}",
                                    userId, model.IdDocumentType);
                            }

                            _logger.LogInformation(
                                "Document verification completed for user {UserId}. Source: {Source}, Extracted name: {ExtractedName}, Extracted ID: {ExtractedId}",
                                userId, hasCameraData ? "Camera" : "Upload", documentResult.extractedIdName, documentResult.extractedIdNumber);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error during document verification for user {UserId}", userId);

                            documentData.ExtractedIdName = "OCR_FAILED";
                            documentData.ExtractedIdNumber = "OCR_FAILED";
                            documentData.IdDocumentMessage = "⚠️ We couldn't process your document automatically. This may be due to image quality, lighting, or document clarity. Please try uploading a clearer image or contact support for manual verification.";
                            documentData.IdDocumentVerified = false;
                            documentData.IdDocumentConfidence = 0.0f;

                            ViewBag.ProcessingError = documentData.IdDocumentMessage;
                            _logger.LogWarning("Stored image with OCR failure placeholders for user {UserId}", userId);
                        }

                        HttpContext.Session.SetString("DocumentData", System.Text.Json.JsonSerializer.Serialize(documentData));
                        _logger.LogInformation("New document data stored in session for user {UserId}, image length: {Length}",
                            userId, imageData.Length);
                    }

                    if (!string.IsNullOrEmpty(ViewBag.ValidationWarning as string))
                    {
                        // Restore form data for user
                        ViewBag.StoredImageData = documentData.IdDocumentImageData;
                        ViewBag.StoredImageContentType = documentData.IdDocumentImageContentType;
                        ViewBag.HasStoredImage = true;
                        ViewBag.ExtractedIdName = documentData.ExtractedIdName;
                        ViewBag.HasExtractedData = !string.IsNullOrEmpty(documentData.ExtractedIdNumber);

                        model.ExtractedIdNumber = documentData.ExtractedIdNumber;
                        model.ExtractedIdName = documentData.ExtractedIdName;
                        model.ExtractedExpiryDate = documentData.ExtractedExpiryDate;

                        return View(model);
                    }

                    return RedirectToAction("Verify");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during document verification");
                ModelState.AddModelError("", "An error occurred during document verification. Please try again.");
                return View(model);
            }
        }

        private string GetDocumentValidationMessage(string selectedDocumentType, string extractedIdNumber, string extractedIdName, string message)
        {
            // PRIORITY 1: Check for document type mismatch FIRST - even if other extraction failed
            // This handles cases where we can extract SOME text but it doesn't match the selected type
            if (!string.IsNullOrEmpty(extractedIdNumber))
            {
                var detectedDocumentType = DetectDocumentTypeFromIdNumber(extractedIdNumber);
                if (!string.IsNullOrEmpty(detectedDocumentType) &&
                    !string.Equals(detectedDocumentType, selectedDocumentType, StringComparison.OrdinalIgnoreCase))
                {
                    return $"<strong>Document type mismatch detected!</strong><br/>The uploaded document appears to be a <strong>{detectedDocumentType}</strong>, but you selected <strong>{selectedDocumentType}</strong>.<br/>Please either:<br/>• Select the correct document type (<strong>{detectedDocumentType}</strong>), or<br/>• Upload the correct document type ({selectedDocumentType})";
                }
            }

            // PRIORITY 2: Check if we extracted SOMETHING but it doesn't match any known pattern
            // This handles cases where OCR works but the extracted ID doesn't match any expected format
            if (!string.IsNullOrEmpty(extractedIdNumber) && !string.IsNullOrEmpty(extractedIdName))
            {
                // If we have both but no detected type, it might be unclear text or wrong document type
                var detectedType = DetectDocumentTypeFromIdNumber(extractedIdNumber);
                if (string.IsNullOrEmpty(detectedType))
                {
                    return $"<strong>Unclear document type detected.</strong><br/>We extracted information from your document, but the ID format doesn't match standard {selectedDocumentType} patterns.<br/>Please ensure you're uploading the correct document type or try a clearer image.";
                }
            }

            // PRIORITY 3: Check if no text was detected at all
            if (string.IsNullOrEmpty(extractedIdNumber) && string.IsNullOrEmpty(extractedIdName))
            {
                return "No readable text was detected in your document. Please ensure the image is clear, well-lit, and all text is visible. Try taking another photo with better lighting.";
            }

            // PRIORITY 4: Check for partial extraction issues
            if (!string.IsNullOrEmpty(extractedIdNumber) && string.IsNullOrEmpty(extractedIdName))
            {
                return "Only partial information could be extracted. Your ID number was detected, but the name is unclear. Please try uploading a clearer image or ensure all text is visible.";
            }

            if (string.IsNullOrEmpty(extractedIdNumber) && !string.IsNullOrEmpty(extractedIdName))
            {
                return "Only partial information could be extracted. Your name was detected, but the ID number is unclear. Please try uploading a clearer image of your document.";
            }

            // PRIORITY 5: Return original message if no specific issues detected
            return message;
        }

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

        [HttpGet]
        public IActionResult Verify()
        {
            var sessionDocumentData = HttpContext.Session.GetString("DocumentData");
            if (!string.IsNullOrEmpty(sessionDocumentData))
            {
                try
                {
                    var documentData = System.Text.Json.JsonSerializer.Deserialize<DocumentVerificationData>(sessionDocumentData);

                    // Check if we have valid extracted data (not OCR failure placeholders)
                    var hasValidExtractedData = !string.IsNullOrEmpty(documentData.ExtractedIdNumber) &&
                                               documentData.ExtractedIdNumber != "OCR_FAILED";

                    if (!hasValidExtractedData)
                    {
                        _logger.LogWarning("Verify GET: No valid extracted data found, redirecting to Document");
                        TempData["ErrorMessage"] = "Document processing incomplete. Please verify your document again.";
                        return RedirectToAction("Document");
                    }

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
            TempData["ErrorMessage"] = "No document data found. Please complete document verification first.";
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
                    documentData.IdDocumentImageData
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

        [JsonPropertyName("extractedExpiryDate")]
        public DateTime? ExtractedExpiryDate { get; set; }
    }
}