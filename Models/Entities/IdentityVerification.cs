using System.ComponentModel.DataAnnotations;

namespace Freelancing.Models.Entities
{
    public class IdentityVerification
    {
        public Guid Id { get; set; }
        
        [Required]
        public string UserAccountId { get; set; }
        public UserAccount UserAccount { get; set; }
        
        // ID Document Verification
        public string? IdDocumentType { get; set; } // "PASSPORT", "DRIVERS_LICENSE", "NATIONAL_ID", "OTHERS"
        public DateTime? IdDocumentExpiryDate { get; set; }
        public bool? IdDocumentVerified { get; set; }
        public float? IdDocumentConfidence { get; set; }
        
        // Face Verification
        public bool? FaceVerified { get; set; }
        public float? FaceConfidence { get; set; }
        
        // Verification Status
        [Required]
        public string Status { get; set; } // "PENDING", "APPROVED", "REJECTED"
        public string? RejectionReason { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public DateTime? RejectedAt { get; set; }
        
        // Encryption Fields
        public bool IsEncrypted { get; set; } = true;
        public string EncryptionMethod { get; set; } = "AES-256";
        
        // Encrypted Data Fields (for sensitive information)
        public string? EncryptedIdDocumentNumber { get; set; }
        public string? EncryptedIdDocumentImage { get; set; }
        public string? EncryptedFaceImage { get; set; }
        
        // Metadata
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
