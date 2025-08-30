using Freelancing.Models.Entities;

namespace Freelancing.Models
{
    public class ContractViewModel
    {
        public Guid Id { get; set; }
        public string ContractTitle { get; set; }
        public string ContractContent { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastModifiedAt { get; set; }
        
        // Project Information
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string ProjectDescription { get; set; }
        public string ProjectBudget { get; set; }
        public string ProjectCategory { get; set; }
        
        // Client Information
        public string ClientId { get; set; }
        public string ClientName { get; set; }
        public string ClientEmail { get; set; }
        public string? ClientPhoto { get; set; }
        public DateTime? ClientSignedAt { get; set; }
        public string? ClientSignatureData { get; set; }
        public string? ClientSignatureType { get; set; }
        public bool ClientHasSigned => ClientSignedAt.HasValue;
        
        // Freelancer Information
        public string FreelancerId { get; set; }
        public string FreelancerName { get; set; }
        public string FreelancerEmail { get; set; }
        public string? FreelancerPhoto { get; set; }
        public DateTime? FreelancerSignedAt { get; set; }
        public string? FreelancerSignatureData { get; set; }
        public string? FreelancerSignatureType { get; set; }
        public bool FreelancerHasSigned => FreelancerSignedAt.HasValue;
        
        // Bidding Information
        public int AgreedAmount { get; set; }
        public string DeliveryTimeline { get; set; }
        public string Proposal { get; set; }
        
        // Contract Terms (parsed from JSON)
        public PaymentTermsViewModel? PaymentTerms { get; set; }
        public List<string>? DeliverableRequirements { get; set; }
        public RevisionPolicyViewModel? RevisionPolicy { get; set; }
        public TimelineViewModel? Timeline { get; set; }
        
        // Document Information
        public string? DocumentPath { get; set; }
        public bool HasSignedDocument => !string.IsNullOrEmpty(DocumentPath);
        
        // Project Completion Tracking
        public DateTime? ClientMarkedCompleteAt { get; set; }
        public DateTime? FreelancerMarkedCompleteAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool ClientMarkedComplete => ClientMarkedCompleteAt.HasValue;
        public bool FreelancerMarkedComplete => FreelancerMarkedCompleteAt.HasValue;
        public bool IsCompleted => Status == "Completed";
        
        // Deliverable Information
        public int AcceptedDeliverablesCount { get; set; }
        public bool HasSufficientDeliverables => AcceptedDeliverablesCount >= 1;
        
        // Status Helpers
        public bool IsFullySigned => ClientHasSigned && FreelancerHasSigned;
        public bool IsActive => Status == "Active";
        public bool CanBeModified => Status == "Draft" || Status == "AwaitingFreelancer" || Status == "AwaitingClient";
        public bool CanShowFinishButton => IsActive && HasSufficientDeliverables;
        
        // Audit Information
        public List<ContractAuditLogViewModel> AuditLogs { get; set; } = new List<ContractAuditLogViewModel>();
        public List<ContractRevisionViewModel> Revisions { get; set; } = new List<ContractRevisionViewModel>();
    }

    public class PaymentTermsViewModel
    {
        public int UpfrontPercentage { get; set; }
        public int FinalPercentage { get; set; }
        public List<MilestoneViewModel> Milestones { get; set; } = new List<MilestoneViewModel>();
    }

    public class MilestoneViewModel
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public int Percentage { get; set; }
        public DateTime? DueDate { get; set; }
    }

    public class RevisionPolicyViewModel
    {
        public int FreeRevisions { get; set; }
        public decimal AdditionalRevisionCost { get; set; }
        public string RevisionScope { get; set; }
    }

    public class TimelineViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime Deadline { get; set; }
        public List<MilestoneViewModel> Milestones { get; set; } = new List<MilestoneViewModel>();
    }

    public class ContractAuditLogViewModel
    {
        public Guid Id { get; set; }
        public string UserName { get; set; }
        public string Action { get; set; }
        public string? Details { get; set; }
        public DateTime Timestamp { get; set; }
        public string? IPAddress { get; set; }
    }

    public class ContractRevisionViewModel
    {
        public Guid Id { get; set; }
        public int RevisionNumber { get; set; }
        public string RevisionNotes { get; set; }
        public string CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

