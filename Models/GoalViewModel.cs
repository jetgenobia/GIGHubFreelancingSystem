using Freelancing.Models.Entities;

namespace Freelancing.Models
{
    public class GoalViewModel
    {
        public Guid MatchId { get; set; }
        public string PartnerName { get; set; }
        public bool IsCurrentUserMentor { get; set; }
        public List<GoalItemViewModel> Goals { get; set; } = new List<GoalItemViewModel>();
        public int TotalGoals { get; set; }
        public int CompletedGoals { get; set; }
        public double ProgressPercentage => TotalGoals > 0 ? (double)CompletedGoals / TotalGoals * 100 : 0;
    }

    public class GoalItemViewModel
    {
        public Guid GoalId { get; set; }
        public string GoalName { get; set; }
        public string GoalDescription { get; set; }
        public int Order { get; set; }
        public bool IsCompletedByMentor { get; set; }
        public bool IsCompletedByMentee { get; set; }
        public bool IsFullyCompleted { get; set; }
        public bool CanMarkAsDone { get; set; } // Whether the current user can mark this goal as done
        public bool ShowMarkAsDoneButton { get; set; } // Whether to show the mark as done button
        public string CompletedBy { get; set; } // "Mentor", "Mentee", or "Both"
        public DateTime? CompletedAt { get; set; }
        public string? IconSvg { get; set; } // Custom SVG icon for the goal
        public bool IsCustomGoal { get; set; }
        public string? Priority { get; set; }
        public DateTime? TargetDate { get; set; }
        public bool IsOverdue { get; set; }
        public string? Category { get; set; }
        public List<string> SuccessCriteria { get; set; } = new List<string>();
        public bool CanDelete { get; set; }
    }
}
