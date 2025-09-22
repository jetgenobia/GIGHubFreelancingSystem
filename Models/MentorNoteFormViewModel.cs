using System.ComponentModel.DataAnnotations;

namespace Freelancing.Models
{
    public class MentorNoteFormViewModel
    {
        public Guid? Id { get; set; }

        public Guid MatchId { get; set; }
        public Guid GoalId { get; set; }
        public string GoalName { get; set; }
        public string GoalDescription { get; set; }
        public string MenteeName { get; set; }

        [Required(ErrorMessage = "Please provide notes about this session.")]
        [StringLength(2000, ErrorMessage = "Notes cannot exceed 2000 characters.")]
        [Display(Name = "Session Notes")]
        public string Notes { get; set; }

        [StringLength(1000, ErrorMessage = "Feedback cannot exceed 1000 characters.")]
        [Display(Name = "Feedback for Mentee")]
        public string? Feedback { get; set; }

        [Range(1, 5, ErrorMessage = "Please rate between 1 and 5.")]
        [Display(Name = "Progress Rating (1-5)")]
        public int? ProgressRating { get; set; }

        [Display(Name = "Assign Task")]
        public bool IsTaskAssigned { get; set; }

        [StringLength(2000, ErrorMessage = "Task title cannot exceed 2000 characters.")]
        [Display(Name = "Task Title")]
        public string? TaskTitle { get; set; }

        [StringLength(2000, ErrorMessage = "Task description exceed 2000 characters.")]
        [Display(Name = "Task Instruction")]
        public string? TaskDescription { get; set; }

        public bool IsCompletedByMentor { get; set; }
        public bool IsCompletedByMentee { get; set; }
        public bool IsFullyCompleted { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}