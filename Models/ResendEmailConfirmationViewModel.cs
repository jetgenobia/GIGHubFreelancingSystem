using System.ComponentModel.DataAnnotations;

namespace Freelancing.Models
{
    public class ResendEmailConfirmationViewModel
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;
    }
}