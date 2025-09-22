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
        public bool CanMarkAsDone { get; set; }
        public bool ShowMarkAsDoneButton { get; set; }
        public string CompletedBy { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string IconSvg { get; set; }
        public bool IsFullyCompleted { get; set; }

        // New properties for evidence and note tracking
        public bool HasMenteeEvidence { get; set; }
        public bool HasMentorNote { get; set; }
        public bool IsCurrentUserMentor { get; set; }

        // Custom goal properties
        public bool IsCustomGoal { get; set; }
        public string? Priority { get; set; }
        public DateTime? TargetDate { get; set; }
        public bool IsOverdue { get; set; }
        public string? Category { get; set; }
        public List<string> SuccessCriteria { get; set; } = new List<string>();
        public bool CanDelete { get; set; }
    }
}
