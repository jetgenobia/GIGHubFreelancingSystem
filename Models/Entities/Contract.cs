namespace Freelancing.Models.Entities
{
    public class Contract
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Project Project { get; set; }
        public Guid BiddingId { get; set; }
        public Bidding Bidding { get; set; }
        
        // Contract Content
        public string ContractTitle { get; set; }
        public string ContractContent { get; set; } // HTML content
        public string? ContractTemplateUsed { get; set; }
        
        // Contract Status and Dates
        public string Status { get; set; } = "Draft"; // Draft, AwaitingFreelancer, AwaitingClient, Active, Completed, Cancelled, Terminated
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastModifiedAt { get; set; }
        public DateTime? TerminatedAt { get; set; }
        
        // Client Signature Information
        public DateTime? ClientSignedAt { get; set; }
        public string? ClientSignatureType { get; set; } // "Canvas", "Text", "Upload"
        public string? ClientSignatureData { get; set; } // Base64 for canvas, text for typed
        public string? ClientIPAddress { get; set; }
        public string? ClientUserAgent { get; set; }
        
        // Freelancer Signature Information
        public DateTime? FreelancerSignedAt { get; set; }
        public string? FreelancerSignatureType { get; set; }
        public string? FreelancerSignatureData { get; set; }
        public string? FreelancerIPAddress { get; set; }
        public string? FreelancerUserAgent { get; set; }
        
        // Contract Terms (JSON stored as strings for flexibility)
        public string? PaymentTerms { get; set; } // JSON: { "upfront": 30, "milestones": [...], "final": 70 }
        public string? DeliverableRequirements { get; set; } // JSON array of requirements
        public string? RevisionPolicy { get; set; } // JSON: { "freeRevisions": 3, "additionalCost": 50 }
        
        // Project Completion Tracking
        public DateTime? ClientMarkedCompleteAt { get; set; }
        public DateTime? FreelancerMarkedCompleteAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? Timeline { get; set; } // JSON: { "startDate": "...", "milestones": [...], "deadline": "..." }
        
        // Document Management
        public string? DocumentPath { get; set; } // Path to signed PDF
        public string? DocumentHash { get; set; } // SHA256 hash for integrity verification
        public long? DocumentSize { get; set; }
        
        // Relationships
        public ICollection<ContractAuditLog> AuditLogs { get; set; } = new List<ContractAuditLog>();
        public ICollection<ContractRevision> Revisions { get; set; } = new List<ContractRevision>();
    }
}

