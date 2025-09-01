using System.ComponentModel.DataAnnotations;

namespace Freelancing.Models
{
    public class PortfolioItem
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        public string? RepositoryLink { get; set; }

        public List<IFormFile>? Files { get; set; }
    }
}
