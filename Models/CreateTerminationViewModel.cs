using System.ComponentModel.DataAnnotations;

namespace Freelancing.Models
{
    public class CreateTerminationViewModel
    {
        public Guid ContractId { get; set; }
        public string ContractTitle { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string FreelancerName { get; set; } = string.Empty;
        public decimal AgreedAmount { get; set; }
        public string ContractStatus { get; set; } = string.Empty;
        public bool IsClient { get; set; }
        public bool IsFreelancer { get; set; }
        
        [Required(ErrorMessage = "Please provide a reason for termination")]
        [StringLength(200, ErrorMessage = "Reason must be between 10 and 200 characters", MinimumLength = 10)]
        public string TerminationReason { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Please provide detailed explanation for termination")]
        [StringLength(2000, ErrorMessage = "Details must be between 50 and 2000 characters")]
        public string TerminationDetails { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Please specify the final payment amount")]
        [Range(0, double.MaxValue, ErrorMessage = "Final payment must be a positive number")]
        public decimal FinalPayment { get; set; }
        
        [StringLength(1000, ErrorMessage = "Settlement notes must not exceed 1000 characters")]
        public string? SettlementNotes { get; set; }
        
        [Required(ErrorMessage = "Please confirm that you understand the implications")]
        public bool UnderstandImplications { get; set; }
        
        [Required(ErrorMessage = "Please confirm that you agree to the termination terms")]
        public bool AgreeToTerms { get; set; }
    }
}
