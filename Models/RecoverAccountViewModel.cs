using System.ComponentModel.DataAnnotations;

namespace Freelancing.Models
{
    public class RecoverAccountViewModel
    {
        [Required]
        public string UserId { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }
}