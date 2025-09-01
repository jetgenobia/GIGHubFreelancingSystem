using Freelancing.Models.Entities;

namespace Freelancing.Models
{
    public class SearchViewModel
    {
        public string SearchTerm { get; set; } = string.Empty;
        public string SearchType { get; set; } = "all"; // "all", "users", "projects"
        public List<UserAccount> Users { get; set; } = new List<UserAccount>();
        public List<Project> Projects { get; set; } = new List<Project>();
        public bool HasResults => Users.Any() || Projects.Any();
        public int TotalResults => Users.Count + Projects.Count;
    }
}
