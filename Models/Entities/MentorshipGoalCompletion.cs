using System.ComponentModel.DataAnnotations;

namespace Freelancing.Models.Entities
{
    public class MentorshipGoalCompletion
    {
        public Guid Id { get; set; }

        [Required]
        public Guid MentorshipMatchId { get; set; }
        public MentorshipMatch MentorshipMatch { get; set; }

        [Required]
        public Guid GoalId { get; set; }
        public Goal Goal { get; set; }

        [Required]
        public string CompletedByUserId { get; set; }
        public UserAccount CompletedByUser { get; set; }

        [Required]
        public DateTime CompletedAt { get; set; }

        [Required]
        [StringLength(20)]
        public string CompletionType { get; set; } // "Mentor" or "Mentee"

        // Navigation properties for easy querying
        public bool IsCompletedByMentor { get; set; }
        public bool IsCompletedByMentee { get; set; }
        public bool IsFullyCompleted => IsCompletedByMentor && IsCompletedByMentee;
    }
}
