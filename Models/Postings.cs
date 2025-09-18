using Freelancing.Models.Entities;

namespace Freelancing.Models
{
    public class Postings
    {
        public List<Project> Projects { get; set; } = new();
        public int OpenProjects { get; set; }
        public int ClosedProjects { get; set; }
    }
}
