namespace Freelancing.Models.Entities
{
    public class Deliverable
    {
        public Guid Id { get; set; }
        public Guid ContractId { get; set; }
        public Contract Contract { get; set; }
        public string SubmittedByUserId { get; set; }
        public UserAccount SubmittedByUser { get; set; }
        
        // Deliverable Details
        public string Title { get; set; }
        public string Status { get; set; } = "Submitted"; // Submitted, Approved, For Revision, In Progress
        
        // File Uploads
        public string? SubmittedFilesPaths { get; set; } // JSON array of file paths
        public string? RepositoryLinks { get; set; } // JSON array of repository/drive links
        
        // Timestamps
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow.ToLocalTime();
        public DateTime? ReviewedAt { get; set; }
        
        // Review Information
        public string? ReviewComments { get; set; }
        public string? ReviewedByUserId { get; set; }
        public UserAccount? ReviewedByUser { get; set; }
        
        // Version Control
        public int Version { get; set; } = 1;
        public Guid? PreviousVersionId { get; set; }
        public Deliverable? PreviousVersion { get; set; }
    }
}
