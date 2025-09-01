using Freelancing.Models.Entities;

namespace Freelancing.Models
{
    public class EditAccount
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string UserName { get; set; }
        public string? Photo { get; set; }
        public string? Bio { get; set; }
        public string? ExperienceLevel { get; set; }
        public List<UserSkill> SavedSkills { get; set; } = new List<UserSkill>();
        public List<Portfolio> Portfolios { get; set; } = new List<Portfolio>();
        public Guid UserId { get; set; }
        public int TotalSkillsCount { get; set; }
        public bool HasCompletedMentorshipAsMentor { get; set; } // True if user has completed mentorship as a mentor
        public bool HasCompletedMentorshipAsMentee { get; set; } // True if user has completed mentorship as a mentee
    }
}
