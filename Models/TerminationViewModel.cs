namespace Freelancing.Models
{
    public class TerminationViewModel
    {
        public Guid Id { get; set; }
        public Guid ContractId { get; set; }
        public string ContractTitle { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string FreelancerName { get; set; } = string.Empty;
        public decimal AgreedAmount { get; set; }
        public string TerminationReason { get; set; } = string.Empty;
        public string TerminationDetails { get; set; } = string.Empty;
        public decimal FinalPayment { get; set; }
        public string? SettlementNotes { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
        public string RequestedByRole { get; set; } = string.Empty;
        public string RequestedByName { get; set; } = string.Empty;
        
        // Signature Status
        public bool ClientHasSigned { get; set; }
        public DateTime? ClientSignedAt { get; set; }
        public bool FreelancerHasSigned { get; set; }
        public DateTime? FreelancerSignedAt { get; set; }
        
        // Document Status
        public bool HasSignedDocument { get; set; }
        public string? DocumentPath { get; set; }
        
        // User Access
        public string ClientId { get; set; }
        public string FreelancerId { get; set; }
        public string RequestedByUserId { get; set; }
        public bool CanUserSign { get; set; }
        public bool CanUserCancel { get; set; }
        public bool CanUserDownload { get; set; }
        public bool CanUserExecute { get; set; }
    }
}
