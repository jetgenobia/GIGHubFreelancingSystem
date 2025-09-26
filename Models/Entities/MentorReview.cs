using System.ComponentModel.DataAnnotations;

namespace Freelancing.Models.Entities
{
    public class MentorReview
    {
        public Guid Id { get; set; }

        [Required]
        public Guid MentorshipMatchId { get; set; }
        public MentorshipMatch MentorshipMatch { get; set; }

        [Required]
        public string MentorId { get; set; }
        public UserAccount Mentor { get; set; }

        [Required]
        public string MenteeId { get; set; }
        public UserAccount Mentee { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; } // 1-5 stars

        [Required]
        public bool WouldRecommend { get; set; }

        [StringLength(2000)]
        public string? Comments { get; set; }

        [StringLength(500)]
        public string? Strengths { get; set; }

        [StringLength(500)]
        public string? AreasForImprovement { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


    }
}
