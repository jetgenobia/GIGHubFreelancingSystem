using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace Freelancing.Models
{
    // View Model for Document Step (Step 2)
    public class DocumentVerificationViewModel : IValidatableObject
    {
        // ID Document Fields
        [Required(ErrorMessage = "Please select an ID document type")]
        public string IdDocumentType { get; set; }

        // Read-only property for extracted ID number from OCR
        // This should be preserved across form submissions
        public string? ExtractedIdNumber { get; set; }

        // File upload is required for new users, optional for returning users
        public IFormFile? IdDocumentImage { get; set; }

        [DataType(DataType.Date)]
        public DateTime? IdDocumentExpiryDate { get; set; }

        public bool IdDocumentHasNoExpiration { get; set; }

        // Hidden field to preserve extracted ID name
        public string? ExtractedIdName { get; set; }

        // NEW: Property to hold extracted expiration date from OCR
        public DateTime? ExtractedExpiryDate { get; set; }

        public string? OriginalIdType { get; set; }
        public bool ReprocessDocument { get; set; }

        // Custom validation for expiry date and file upload
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // For National ID, automatically set no expiration - validation passes
            if (string.Equals(IdDocumentType, "National ID", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(IdDocumentType, "NATIONAL_ID", StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            bool hasIdTypeChanged = !string.IsNullOrEmpty(OriginalIdType) &&
                               OriginalIdType != IdDocumentType;

            // NEW: If ID type changed, always require new image upload
            if (hasIdTypeChanged)
            {
                var cameraImageData = GetCameraImageData(validationContext);
                bool hasCameraData = !string.IsNullOrEmpty(cameraImageData) && cameraImageData.Length > 1000;

                if (IdDocumentImage == null && !hasCameraData)
                {
                    yield return new ValidationResult(
                        $"ID type changed to {IdDocumentType}. Please upload your {IdDocumentType} document or capture it using the camera.",
                        new[] { nameof(IdDocumentImage) });
                }
                yield break; // Skip session check for ID type changes
            }

            // CRITICAL FIX: Check session data first
            var httpContext = validationContext.GetService(typeof(IHttpContextAccessor)) as IHttpContextAccessor;
            var sessionData = httpContext?.HttpContext?.Session?.GetString("DocumentData");

            // If user has session data, they're a returning user - skip file validation
            if (!string.IsNullOrEmpty(sessionData))
            {
                yield break; // Allow returning users to change other fields without new upload
            }

            // Only validate file upload for truly new users (no session data, no ID type change)
            var cameraData = GetCameraImageData(validationContext);
            bool hasCameraUpload = !string.IsNullOrEmpty(cameraData) && cameraData.Length > 1000;

            if (IdDocumentImage == null && !hasCameraUpload)
            {
                yield return new ValidationResult("Please upload your ID document or capture it using the camera.",
                    new[] { nameof(IdDocumentImage) });
            }
        }
        private string GetCameraImageData(ValidationContext validationContext)
        {
            try
            {
                var httpContext = validationContext.GetService(typeof(IHttpContextAccessor)) as IHttpContextAccessor;
                return httpContext?.HttpContext?.Request?.Form["DocumentImageData"].ToString() ?? "";
            }
            catch
            {
                return "";
            }
        }
    }

    // View Model for Face Verification Step (Step 3)
    public class FaceVerificationViewModel
    {
        // Document data passed from previous step
        [Required]
        public string IdDocumentType { get; set; }

        public string? ExtractedIdNumber { get; set; }
        public DateTime? IdDocumentExpiryDate { get; set; }
        public bool IdDocumentHasNoExpiration { get; set; }
        public string? ExtractedIdName { get; set; }

        // Face Verification Fields - Live Capture
        [Required(ErrorMessage = "Please capture a live photo of your face")]
        public string LiveFaceImageData { get; set; } // Base64 encoded image data

        // Terms and Conditions
        [Required(ErrorMessage = "You must agree to the terms and conditions")]
        public bool AgreeToTerms { get; set; }
    }

    // Legacy view model for backward compatibility (updated to use extracted ID)
    public class IdentityVerificationViewModel
    {
        // ID Document Fields
        [Required(ErrorMessage = "Please select an ID document type")]
        public string IdDocumentType { get; set; }

        // Read-only property for extracted ID number instead of manual input
        public string? ExtractedIdNumber { get; set; }

        [Required(ErrorMessage = "Please upload your ID document image")]
        public IFormFile IdDocumentImage { get; set; }

        [DataType(DataType.Date)]
        public DateTime? IdDocumentExpiryDate { get; set; } = DateTime.Today; // Default to today, nullable
        public DateTime? ExtractedExpiryDate { get; set; }

        public bool IdDocumentHasNoExpiration { get; set; } // Checkbox for IDs without expiration

        // Face Verification Fields - Live Capture
        public string? LiveFaceImageData { get; set; } // Made optional for Document step

        // Terms and Conditions
        public bool AgreeToTerms { get; set; }

        // Hidden field to preserve extracted ID name
        public string? ExtractedIdName { get; set; }

        // NEW: base64 image held in session (optional)
        public string? StoredIdDocumentImageData { get; set; } // Backing property used by SaveVerificationDataAsync
    }

    public class VerificationStatusViewModel
    {
        public Guid Id { get; set; }
        public string Status { get; set; }
        public string? IdDocumentType { get; set; }
        public bool? IdDocumentVerified { get; set; }
        public float? IdDocumentConfidence { get; set; }
        public bool? FaceVerified { get; set; }
        public float? FaceConfidence { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public DateTime? RejectedAt { get; set; }
        public string? RejectionReason { get; set; }
        public bool IsVerified => Status == "APPROVED";
        public bool CanPostProject => IsVerified;
        public bool IsEncrypted { get; set; }
        public string EncryptionMethod { get; set; }
    }

    public class VerificationResultViewModel
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Status { get; set; }
        public Guid VerificationId { get; set; }
        public string? RejectionReason { get; set; }
        public float? FaceSimilarityScore { get; set; }
        public string? ExtractedIdNumber { get; set; } // Using extracted ID number
        public string? IdDocumentType { get; set; }
        public bool? IdDocumentVerified { get; set; }
        public string? IdDocumentMessage { get; set; }
        public float? IdDocumentConfidence { get; set; }
        public bool? FaceVerified { get; set; }
        public string? FaceMessage { get; set; }
        public float? FaceConfidence { get; set; }
        public string? ExtractedIdName { get; set; }
    }

    public class LiveFaceCaptureViewModel
    {
        public string UserId { get; set; }
        public string IdDocumentType { get; set; }
        public string? ExtractedIdNumber { get; set; } // Using extracted ID number
        public DateTime? IdDocumentExpiryDate { get; set; }
        public bool IdDocumentHasNoExpiration { get; set; }
    }
}