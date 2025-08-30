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
        
        public Guid? MentorshipId { get; set; }
        
        public PeerMentorship? Mentorship { get; set; }
        
        public ICollection<Project> Projects { get; set; }
        public ICollection<Bidding> Biddings { get; set; }
        public virtual ICollection<UserAccountSkill> UserAccountSkills { get; set; } = new List<UserAccountSkill>();
    }
}
