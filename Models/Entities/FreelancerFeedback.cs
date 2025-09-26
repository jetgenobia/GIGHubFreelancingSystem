using System.ComponentModel.DataAnnotations;

namespace Freelancing.Models.Entities
{
    public class FreelancerFeedback
    {

        public Guid Id { get; set; }

        [Required]
        public Guid AcceptBidId { get; set; }
        public Bidding AcceptBidding { get; set; }

        [Required]
        public string FreelancerId { get; set; }
        public UserAccount Freelancer { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; } // 1-5 stars

        [Required]
        public bool WouldRecommend { get; set; }

        [StringLength(2000)]
        public string? Comments { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
