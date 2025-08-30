using System.ComponentModel.DataAnnotations;

namespace Freelancing.Models.Entities
{
    public class MentorshipSession
    {
        public Guid Id { get; set; }

        [Required]
        public Guid MentorshipMatchId { get; set; }
        public MentorshipMatch MentorshipMatch { get; set; }

        [Required]
        public string CreatedByUserId { get; set; }

        [Required]
        public DateTime ScheduledStartUtc { get; set; }

        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = "Proposed"; // Proposed, Confirmed, Completed, Cancelled

        [MaxLength(150)]
        public string? Title { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }

        [MaxLength(100)]
        public string? TimeZone { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }
    }
}



