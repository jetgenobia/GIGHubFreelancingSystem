using System.ComponentModel.DataAnnotations;

namespace Freelancing.Models
{
    public class EnableAuthenticatorViewModel
    {
        // Display-only values filled by controller
        public string SharedKey { get; set; } = string.Empty;
        public string AuthenticatorUri { get; set; } = string.Empty;
        public string QrCodeImageUrl { get; set; } = string.Empty;

        // User-entered verification code
        [Required]
        [Display(Name = "Verification code")]
        public string VerificationCode { get; set; } = string.Empty;
    }
}