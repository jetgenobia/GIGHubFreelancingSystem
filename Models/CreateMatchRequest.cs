using System.ComponentModel.DataAnnotations;
using Freelancing.Models.Entities;

namespace Freelancing.Models
{
    public class CreateMatchRequest
    {
        [Required]
        public string PartnerId { get; set; }

        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
        [Display(Name = "Additional Notes")]
        public string Notes { get; set; } = string.Empty;

        public List<UserAccount> Prospect { get; set; } = new();
        public List<UserSkill> UserSkills { get; set; } = new();

        // Add the partner/mentor being requested
        public UserAccount? Partner { get; set; }

        // Optional: Add selected skills for the mentorship focus
        [Display(Name = "Skills to focus on")]
        public List<Guid> SelectedSkillIds { get; set; } = new();

        // Optional: Add mentorship type
        [Display(Name = "Mentorship Type")]
        public MentorshipType MentorshipType { get; set; } = MentorshipType.General;
    }

    public enum MentorshipType
    {
        General,
        SkillSpecific,
        ProjectBased,
        CareerGuidance
    }
}
