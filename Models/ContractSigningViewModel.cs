using System.ComponentModel.DataAnnotations;

namespace Freelancing.Models
{
    public class ContractSigningViewModel
    {
        public Guid ContractId { get; set; }
        public string ContractTitle { get; set; }
        public string ContractContent { get; set; }
        public string Status { get; set; }
        
        // Project and parties information
        public string ProjectName { get; set; }
        public string ClientName { get; set; }
        public string FreelancerName { get; set; }
        public int AgreedAmount { get; set; }
        public string DeliveryTimeline { get; set; }
        
        // Current user context
        public string CurrentUserId { get; set; }
        public bool IsClient { get; set; }
        public bool IsFreelancer { get; set; }
        public bool CanSign { get; set; }
        public bool HasAlreadySigned { get; set; }
        
        // Other party signature status
        public bool OtherPartyHasSigned { get; set; }
        public string? OtherPartySignedAt { get; set; }
        
        // Signature data (populated after signing)
        [Required(ErrorMessage = "Signature is required")]
        public string? SignatureData { get; set; }
        
        [Required(ErrorMessage = "Signature type is required")]
        public string? SignatureType { get; set; } // "Canvas", "Text"
        
        // For typed signatures
        public string? TypedSignature { get; set; }
        
        // Agreement confirmation
        [Required(ErrorMessage = "You must agree to the contract terms")]
        public bool AgreeToTerms { get; set; }
        
        // Legal disclaimers
        public bool UnderstandLegalImplications { get; set; }
    }
}

