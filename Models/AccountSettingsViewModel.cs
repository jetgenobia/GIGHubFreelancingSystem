using System.ComponentModel.DataAnnotations;

namespace Freelancing.Models
{
    public class AccountSettingsViewModel
    {
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least {2} characters long.")]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation do not match.")]
        public string ConfirmNewPassword { get; set; } = string.Empty;

        public bool ShowEnableAuthenticator { get; set; } = false;

        public string? SharedKey { get; set; }
        public string? AuthenticatorUri { get; set; }
        public string? QrCodeImageUrl { get; set; }

        public string[]? RecoveryCodes { get; set; }

        public bool IsTwoFactorEnabled { get; set; } = false;

        [Display(Name = "Verification code")]
        public string? VerificationCode { get; set; }
    }
}
