namespace Freelancing.Models.Entities
{
    public class ContractRevision
    {
        public Guid Id { get; set; }
        public Guid ContractId { get; set; }
        public Contract Contract { get; set; }
        
        public int RevisionNumber { get; set; }
        public string RevisionContent { get; set; } // HTML content of this revision
        public string? RevisionNotes { get; set; } // Notes about what changed
        
        public string CreatedByUserId { get; set; }
        public UserAccount CreatedByUser { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow.ToLocalTime();
        
        public string? PreviousHash { get; set; } // Hash of previous revision for integrity
        public string CurrentHash { get; set; } // Hash of current revision
    }
}

