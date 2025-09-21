using System.ComponentModel.DataAnnotations;

namespace Freelancing.Models
{
    public class CreateGoalViewModel
    {
        public Guid MatchId { get; set; }

        [Required(ErrorMessage = "Goal name is required")]
        [StringLength(200, ErrorMessage = "Goal name cannot exceed 200 characters")]
        public string GoalName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Goal description is required")]
        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string GoalDescription { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "Category cannot exceed 100 characters")]
        public string? Category { get; set; }

        public string? Priority { get; set; } = "Medium";

        [DataType(DataType.Date)]
        public DateTime? TargetDate { get; set; }

        public List<string>? SuccessCriteria { get; set; } = new List<string>();
    }
}