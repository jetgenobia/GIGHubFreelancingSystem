using System.ComponentModel.DataAnnotations;

namespace Freelancing.Models
{
    public class CancelRegistrationViewModel
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Username")]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "I understand that this will permanently delete my registration")]
        public bool ConfirmCancellation { get; set; }
    }
}