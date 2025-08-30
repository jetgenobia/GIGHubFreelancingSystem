namespace Freelancing.Models.Entities
{
    public class Bidding
    {
        public Guid Id { get; set; }
        public string UserId { get; set; }
        public UserAccount User { get; set; }
        public Guid ProjectId { get; set; }
        public Project Project { get; set; }
        public int Budget { get; set; }
        public string Delivery { get; set; }
        public string Proposal { get; set; }
        public bool IsAccepted { get; set; }
        public DateTime? BiddingAcceptedDate { get; set; }
        public string? PreviousWorksPaths { get; set; } // JSON array of file paths
        public string? RepositoryLinks { get; set; } // JSON array of repository/drive links
    }
}
