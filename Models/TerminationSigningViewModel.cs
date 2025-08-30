using System.ComponentModel.DataAnnotations;

namespace Freelancing.Models
{
    public class TerminationSigningViewModel
    {
        public Guid TerminationId { get; set; }
        public Guid ContractId { get; set; }
        
        // Display Information
        public string TerminationTitle { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string FreelancerName { get; set; } = string.Empty;
        public string TerminationReason { get; set; } = string.Empty;
        public string TerminationDetails { get; set; } = string.Empty;
        public decimal FinalPayment { get; set; }
        public string? SettlementNotes { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
        public string RequestedByRole { get; set; } = string.Empty;
        
        // User Information
        public string CurrentUserId { get; set; }
        public bool IsClient { get; set; }
        public bool IsFreelancer { get; set; }
        public bool CanSign { get; set; }
        public bool HasAlreadySigned { get; set; }
        public bool OtherPartyHasSigned { get; set; }
        public string? OtherPartySignedAt { get; set; }
        
        // Signature Fields
        public string? SignatureData { get; set; }
        public string? SignatureType { get; set; }
        
        // Agreement Fields
        [Required(ErrorMessage = "You must agree to the termination terms")]
        public bool AgreeToTerms { get; set; }
        
        [Required(ErrorMessage = "You must understand the legal implications")]
        public bool UnderstandLegalImplications { get; set; }
    }
}
