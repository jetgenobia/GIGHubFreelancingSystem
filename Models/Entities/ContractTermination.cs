namespace Freelancing.Models.Entities
{
    public class ContractTermination
    {
        public Guid Id { get; set; }
        public Guid ContractId { get; set; }
        public Contract Contract { get; set; }
        
        // Termination Request Details
        public string TerminationReason { get; set; } = string.Empty;
        public string TerminationDetails { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public string RequestedByUserId { get; set; }
        public string RequestedByUserRole { get; set; } = string.Empty; // "Client" or "Freelancer"
        
        // Termination Status
        public string Status { get; set; } = "Pending"; // Pending, AwaitingClient, AwaitingFreelancer, Signed, Cancelled
        public DateTime? CompletedAt { get; set; }
        public decimal FinalPayment { get; set; }
        
        // Client Signature Information
        public DateTime? ClientSignedAt { get; set; }
        public string? ClientSignatureType { get; set; } // "Canvas", "Text"
        public string? ClientSignatureData { get; set; } // Base64 for canvas, text for typed
        public string? ClientIPAddress { get; set; }
        public string? ClientUserAgent { get; set; }
        
        // Freelancer Signature Information
        public DateTime? FreelancerSignedAt { get; set; }
        public string? FreelancerSignatureType { get; set; }
        public string? FreelancerSignatureData { get; set; }
        public string? FreelancerIPAddress { get; set; }
        public string? FreelancerUserAgent { get; set; }
        
        // Termination Terms (JSON stored as strings for flexibility)
        public string? TerminationTerms { get; set; } // JSON: { "effectiveDate": "...", "finalPayment": "...", "deliverables": "..." }
        public string? SettlementDetails { get; set; } // JSON: { "amountPaid": "...", "amountOwed": "...", "refunds": "..." }
        public string? SettlementNotes { get; set; } // Additional notes about settlement terms
        
        // Document Management
        public string? DocumentPath { get; set; } // Path to signed termination PDF
        public string? DocumentHash { get; set; } // SHA256 hash for integrity verification
        public long? DocumentSize { get; set; }
        
        // Audit
        public ICollection<ContractTerminationAuditLog> AuditLogs { get; set; } = new List<ContractTerminationAuditLog>();
    }
}
