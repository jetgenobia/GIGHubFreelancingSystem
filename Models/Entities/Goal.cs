using System.ComponentModel.DataAnnotations;

namespace Freelancing.Models.Entities
{
    public class Goal
    {
        public Guid Id { get; set; }

        [Required]
        [StringLength(200)]
        public string GoalName { get; set; } = string.Empty;

        [StringLength(1000)]
        public string GoalDescription { get; set; } = string.Empty;

        public int Order { get; set; }
        public bool IsActive { get; set; } = true;
        public string? IconSvg { get; set; }

        // Enhanced properties for custom goals
        public bool IsCustom { get; set; } = false;
        public string? Priority { get; set; } // "Low", "Medium", "High"
        public DateTime? TargetDate { get; set; }
        public string? CreatedBy { get; set; } // UserId who created this custom goal
        public DateTime CreatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? Category { get; set; }
        public string? SuccessCriteria { get; set; } // Pipe-separated criteria
    }
}
