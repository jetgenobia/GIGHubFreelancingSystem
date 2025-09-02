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
        
        [Required(ErrorMessage = "Please enter your ID document number")]
        public string IdDocumentNumber { get; set; }
        
        public IFormFile? IdDocumentImage { get; set; }
        
        [DataType(DataType.Date)]
        public DateTime? IdDocumentExpiryDate { get; set; } = DateTime.Today; // Default to today, nullable
        
        public bool IdDocumentHasNoExpiration { get; set; } // Checkbox for IDs without expiration
        
        // Custom validation for expiry date
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!IdDocumentHasNoExpiration && !IdDocumentExpiryDate.HasValue)
            {
                yield return new ValidationResult("Expiry date is required when document has expiration.", new[] { nameof(IdDocumentExpiryDate) });
            }
        }
    }

    // View Model for Face Verification Step (Step 3)
    public class FaceVerificationViewModel
    {
        // Document data passed from previous step
        public string IdDocumentType { get; set; }
        public string IdDocumentNumber { get; set; }
        public DateTime? IdDocumentExpiryDate { get; set; }
        public bool IdDocumentHasNoExpiration { get; set; }
        public string? ExtractedIdName { get; set; }
        public string? ExtractedIdNumber { get; set; }

        // Face Verification Fields - Live Capture
        [Required(ErrorMessage = "Please capture a live photo of your face")]
        public string LiveFaceImageData { get; set; } // Base64 encoded image data
        
        // Terms and Conditions
        [Required(ErrorMessage = "You must agree to the terms and conditions")]
        public bool AgreeToTerms { get; set; }
    }

    // Legacy view model for backward compatibility (can be removed later)
    public class IdentityVerificationViewModel
    {
        // ID Document Fields
        [Required(ErrorMessage = "Please select an ID document type")]
        public string IdDocumentType { get; set; }
        
        [Required(ErrorMessage = "Please enter your ID document number")]
        public string IdDocumentNumber { get; set; }
        
        [Required(ErrorMessage = "Please upload your ID document image")]
        public IFormFile IdDocumentImage { get; set; }
        
        [DataType(DataType.Date)]
        public DateTime? IdDocumentExpiryDate { get; set; } = DateTime.Today; // Default to today, nullable
        
        public bool IdDocumentHasNoExpiration { get; set; } // Checkbox for IDs without expiration
        
        // Face Verification Fields - Live Capture
        public string? LiveFaceImageData { get; set; } // Made optional for Document step
        
        // Terms and Conditions
        public bool AgreeToTerms { get; set; }
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
        public string? IdDocumentNumber { get; set; }
        public string? IdDocumentType { get; set; }
        public bool? IdDocumentVerified { get; set; }
        public string? IdDocumentMessage { get; set; }
        public float? IdDocumentConfidence { get; set; }
        public bool? FaceVerified { get; set; }
        public string? FaceMessage { get; set; }
        public float? FaceConfidence { get; set; }
        public string? ExtractedIdName { get; set; }
        public string? ExtractedIdNumber { get; set; }
    }
    
    public class LiveFaceCaptureViewModel
    {
        public string UserId { get; set; }
        public string IdDocumentType { get; set; }
        public string IdDocumentNumber { get; set; }
        public DateTime? IdDocumentExpiryDate { get; set; }
        public bool IdDocumentHasNoExpiration { get; set; }
    }
}
