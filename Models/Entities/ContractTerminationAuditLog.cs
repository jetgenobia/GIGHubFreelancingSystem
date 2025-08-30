namespace Freelancing.Models.Entities
{
    public class ContractTerminationAuditLog
    {
        public Guid Id { get; set; }
        public Guid ContractTerminationId { get; set; }
        public ContractTermination ContractTermination { get; set; }
        
        public string UserId { get; set; }
        public UserAccount User { get; set; } = null!;
        public string Action { get; set; } = string.Empty; // "Requested", "Signed", "Cancelled", "Viewed", etc.
        public string? Details { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow.ToLocalTime();
        public string? IPAddress { get; set; }
        public string? UserAgent { get; set; }
    }
}
