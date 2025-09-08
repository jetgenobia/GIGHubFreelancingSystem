using System.ComponentModel.DataAnnotations;

namespace Freelancing.Models
{
    public class LoginWith2faViewModel
    {
        [Required]
        [Display(Name = "Authentication code")]
        public string TwoFactorCode { get; set; } = string.Empty;

        [Display(Name = "Remember this device")]
        public bool RememberDevice { get; set; } = false;

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; } = false;

        public string? ReturnUrl { get; set; }
    }
}