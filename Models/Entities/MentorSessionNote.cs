using System.ComponentModel.DataAnnotations;

namespace Freelancing.Models.Entities
{
    public class MentorSessionNote
    {
        public Guid Id { get; set; }

        [Required]
        public Guid MentorshipMatchId { get; set; }

        [Required]
        public Guid GoalId { get; set; }

        [Required]
        public string MentorId { get; set; }

        [Required]
        [StringLength(2000)]
        public string Notes { get; set; }

        [StringLength(1000)]
        public string? Feedback { get; set; }

        // Rating of mentee's progress (1-5)
        [Range(1, 5)]
        public int? ProgressRating { get; set; }

        public bool IsTaskAssigned { get; set; }
        public string? TaskTitle { get; set; }
        public string? TaskDescription { get; set; }

        public DateTime SubmittedAt { get; set; }

        // Navigation properties
        public virtual MentorshipMatch MentorshipMatch { get; set; }
        public virtual Goal Goal { get; set; }
        public virtual UserAccount Mentor { get; set; }
    }
}