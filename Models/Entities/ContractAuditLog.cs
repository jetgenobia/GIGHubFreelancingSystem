namespace Freelancing.Models.Entities
{
    public class ContractAuditLog
    {
        public Guid Id { get; set; }
        public Guid ContractId { get; set; }
        public Contract Contract { get; set; }
        
        public string UserId { get; set; }
        public UserAccount User { get; set; }
        
        public string Action { get; set; } // "Created", "Modified", "Signed", "Viewed", "Downloaded"
        public string? Details { get; set; } // Additional details about the action
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? IPAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? PreviousStatus { get; set; } // For status changes
        public string? NewStatus { get; set; } // For status changes
    }
}

