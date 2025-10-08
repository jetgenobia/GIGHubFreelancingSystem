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

        // Custom validation for expiry date and file upload
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // For National ID, automatically set no expiration - validation passes
            if (string.Equals(IdDocumentType, "National ID", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(IdDocumentType, "NATIONAL_ID", StringComparison.OrdinalIgnoreCase))
            {
                // National IDs don't expire, so we skip expiry validation
                yield break;
            }

            // For other document types, we now rely on extracted expiry dates from OCR
            // If we have extracted data (returning user), we don't need to validate expiry date manually
            // The expiry date should either be extracted or the document should be marked as no expiration

            // Only validate if we don't have extracted data yet (new submission)
            if (string.IsNullOrEmpty(ExtractedIdNumber))
            {
                // This is a new submission - expiry date will be extracted from the uploaded document
                // No need to validate expiry date manually since OCR will handle it

                // File upload validation for new submissions
                if (IdDocumentImage == null)
                {
                    yield return new ValidationResult("Please upload your ID document.", new[] { nameof(IdDocumentImage) });
                }
            }
            else
            {
                // This is a returning user with extracted data
                // At this point, either:
                // 1. ExtractedExpiryDate has a value (expiry date was successfully extracted)
                // 2. ExtractedExpiryDate is null but IdDocumentHasNoExpiration is true (document has no expiry)
                // 3. ExtractedExpiryDate is null and IdDocumentHasNoExpiration is false (expiry not detected - acceptable)

                // We don't need to validate expiry date for returning users since:
                // - The expiry date is either extracted automatically or
                // - The document type doesn't require expiry validation or  
                // - The system will handle missing expiry dates through manual verification

                // No expiry date validation needed here
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