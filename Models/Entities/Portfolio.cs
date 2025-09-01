namespace Freelancing.Models.Entities
{
    public class Portfolio
    {
        public Guid Id { get; set; }
        public string UserId { get; set; }
        public UserAccount User { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string? ProjectImages { get; set; } // JSON array of image paths
        public string? ProjectLink { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
