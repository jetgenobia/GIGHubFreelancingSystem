using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Freelancing.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Username or Email is required.")]
        [DisplayName("Username or Email")]
        public string UserNameorEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, ErrorMessage = "Password must be at least 8 characters long.", MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DisplayName("Remember me")]
        public bool RememberMe { get; set; } = false;
    }
}
