using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Freelancing.Models.Entities
{
    [Index(nameof(Email), IsUnique = true)]
    [Index(nameof(UserName), IsUnique = true)]
    public class UserAccount : IdentityUser
    {
        [Required(ErrorMessage = "First name is required.")]
        public string FirstName { get; set; }
        
        [Required(ErrorMessage = "Last name is required.")]
        public string LastName { get; set; }
        
        public string? Photo { get; set; }
        
        public string Role { get; set; } = "User"; // Default role

        [Required(ErrorMessage = "Freelancer role is required.")]
        public string FRole { get; set; }
        public string? Bio { get; set; }
        public string? ExperienceLevel { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public string? DeletionReason { get; set; }

        public ICollection<Portfolio> Portfolios { get; set; } = new List<Portfolio>();
        public Guid? MentorshipId { get; set; }
        
        public PeerMentorship? Mentorship { get; set; }
        
        public ICollection<Project> Projects { get; set; }
        public ICollection<Bidding> Biddings { get; set; }
        public virtual ICollection<UserAccountSkill> UserAccountSkills { get; set; } = new List<UserAccountSkill>();
    }
}
