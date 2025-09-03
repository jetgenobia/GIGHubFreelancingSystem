using Freelancing.Models.Entities;

namespace Freelancing.Models
{
    public class ClientDashboard
    {
        public List<Project> Projects { get; set; } = new();
        public int TotalProjects { get; set; }
        public int OpenProjects { get; set; }
        public int ClosedProjects { get; set; }
        public List<FreelancerFeedback> freelancerFeedbacks { get; set; } = new();
    }
}
