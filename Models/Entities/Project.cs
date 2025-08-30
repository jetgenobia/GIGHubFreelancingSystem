namespace Freelancing.Models.Entities
{
    public class Project
    {
        public Guid Id { get; set; }
        public ICollection<Bidding> Biddings { get; set; }
        public ICollection<ProjectSkill> ProjectSkills { get; set; } = new List<ProjectSkill>();
        public string UserId { get; set; }
        public UserAccount User { get; set; }
        public string ProjectName { get; set; }
        public string ProjectDescription { get; set; }
        public string Budget { get; set; }
        public string Category { get; set; }
        public string? ImagePaths { get; set; } // JSON array of image paths
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow.ToLocalTime();
        public string Status { get; set; } = "Open"; // Open, Active, Completed, Cancelled
        public Guid? AcceptedBidId { get; set; }
        public Bidding? AcceptedBid { get; set; }
        
        // Contract relationship
        public Contract? Contract { get; set; }
    }
}
