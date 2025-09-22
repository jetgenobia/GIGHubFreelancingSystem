using System.ComponentModel.DataAnnotations;

namespace Freelancing.Models.Entities
{
    public class MentorshipGoalCompletion
    {
        public Guid Id { get; set; }

        [Required]
        public Guid MentorshipMatchId { get; set; }

        [Required]
        public Guid GoalId { get; set; }

        [Required]
        public string CompletedByUserId { get; set; }

        [Required]
        public string CompletionType { get; set; } // "Mentor" or "Mentee"

        public DateTime CompletedAt { get; set; }

        // References to evidence and notes
        public Guid? MenteeEvidenceId { get; set; }
        public Guid? MentorNoteId { get; set; }

        // Navigation properties for easy querying
        public bool IsCompletedByMentor { get; set; }
        public bool IsCompletedByMentee { get; set; }

        // Computed property to maintain backward compatibility
        public bool IsFullyCompleted => IsCompletedByMentor && IsCompletedByMentee;

        // Navigation properties
        public virtual MentorshipMatch MentorshipMatch { get; set; }
        public virtual Goal Goal { get; set; }
        public virtual UserAccount CompletedByUser { get; set; }
        public virtual MenteeSessionEvidence? MenteeEvidence { get; set; }
        public virtual MentorSessionNote? MentorNote { get; set; }
    }
}