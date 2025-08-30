using Freelancing.Models.Entities;
using Microsoft.AspNetCore.Http;

namespace Freelancing.Models
{
    public class AddBidding
    {
        public string UserId { get; set; }
        public Guid ProjectId { get; set; }
        public int Budget { get; set; }
        public string Delivery { get; set; }
        public string Proposal { get; set; }
        public List<IFormFile>? PreviousWorksFiles { get; set; } = new List<IFormFile>();
        public string? PreviousWorksPaths { get; set; } // For existing files when editing
        public string? RepositoryLinks { get; set; } // JSON array of repository/drive links
    }
}
