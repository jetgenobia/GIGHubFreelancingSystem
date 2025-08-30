using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Freelancing.Data;
using Freelancing.Models;
using Freelancing.Models.Entities;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;

namespace Freelancing.Controllers
{
    [Authorize]
    public class ProjectDetailsController : Controller
    {
        private readonly ApplicationDbContext dbContext;

        public ProjectDetailsController(ApplicationDbContext context)
        {
            this.dbContext = context;
        }

        // GET: ProjectDetails/GetProjectInfo/5
        [HttpGet]
        public async Task<IActionResult> GetProjectInfo(Guid projectId)
        {
            var project = await dbContext.Projects
                .Include(p => p.User) // Client
                .Include(p => p.AcceptedBid)
                    .ThenInclude(ab => ab.User) // Freelancer
                .Include(p => p.ProjectSkills)
                    .ThenInclude(ps => ps.UserSkill)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
            {
                return NotFound(new { message = "Project not found" });
            }

            var projectInfo = new
            {
                ProjectId = project.Id,
                ProjectName = project.ProjectName,
                ProjectDescription = project.ProjectDescription,
                ProjectBudget = project.Budget,
                ProjectCategory = project.Category,
                ProjectCreatedAt = project.CreatedAt,
                ProjectStatus = project.Status,
                ProjectImageUrls = ParseJsonArray(project.ImagePaths),
                
                Client = new
                {
                    Id = project.User.Id,
                    Name = $"{project.User.FirstName} {project.User.LastName}",
                    Email = project.User.Email,
                    Photo = project.User.Photo ?? "https://ik.imagekit.io/6txj3mofs/GIGHub%20(11).png?updatedAt=1750552804497"
                },
                
                Freelancer = project.AcceptedBid?.User != null ? new
                {
                    Id = project.AcceptedBid.User.Id,
                    Name = $"{project.AcceptedBid.User.FirstName} {project.AcceptedBid.User.LastName}",
                    Email = project.AcceptedBid.User.Email,
                    Photo = project.AcceptedBid.User.Photo ?? "https://ik.imagekit.io/6txj3mofs/GIGHub%20(11).png?updatedAt=1750552804497"
                } : null,
                
                AcceptedBid = project.AcceptedBid != null ? new
                {
                    Amount = project.AcceptedBid.Budget,
                    Delivery = project.AcceptedBid.Delivery,
                    Proposal = project.AcceptedBid.Proposal
                } : null,
                
                RequiredSkills = project.ProjectSkills?.Select(ps => new
                {
                    Id = ps.UserSkill.Id,
                    Name = ps.UserSkill.Name
                }).Cast<object>().ToList() ?? new List<object>()
            };

            return Json(projectInfo);
        }

        // GET: ProjectDetails/GetClientDetails/5
        [HttpGet]
        public async Task<IActionResult> GetClientDetails(string clientId)
        {
            var client = await dbContext.UserAccounts
                .Include(u => u.UserAccountSkills)
                    .ThenInclude(uas => uas.UserSkill)
                .Include(u => u.Projects)
                .FirstOrDefaultAsync(u => u.Id == clientId && u.Role == "Client");

            if (client == null)
            {
                return NotFound(new { message = "Client not found" });
            }

            var completedProjects = client.Projects.Where(p => p.AcceptedBidId.HasValue).Count();
            var openProjects = client.Projects.Where(p => !p.AcceptedBidId.HasValue).Count();

            var clientDetails = new
            {
                Id = client.Id,
                Name = $"{client.FirstName} {client.LastName}",
                Email = client.Email,
                Photo = client.Photo ?? "https://ik.imagekit.io/6txj3mofs/GIGHub%20(11).png?updatedAt=1750552804497",
                Role = client.Role,
                
                Statistics = new
                {
                    TotalProjects = client.Projects.Count,
                    CompletedProjects = completedProjects,
                    OpenProjects = openProjects
                },
                
                Skills = client.UserAccountSkills?.Select(uas => new
                {
                    Id = uas.UserSkill.Id,
                    Name = uas.UserSkill.Name
                }).Cast<object>().ToList() ?? new List<object>()
            };

            return Json(clientDetails);
        }

        // GET: ProjectDetails/GetFreelancerDetails/5
        [HttpGet]
        public async Task<IActionResult> GetFreelancerDetails(string freelancerId)
        {
            var freelancer = await dbContext.UserAccounts
                .Include(u => u.UserAccountSkills)
                    .ThenInclude(uas => uas.UserSkill)
                .Include(u => u.Biddings)
                    .ThenInclude(b => b.Project)
                .FirstOrDefaultAsync(u => u.Id == freelancerId && u.Role == "Freelancer");

            if (freelancer == null)
            {
                return NotFound(new { message = "Freelancer not found" });
            }

            var completedProjects = await dbContext.Projects
                .Where(p => p.AcceptedBidId.HasValue && 
                           p.Biddings.Any(b => b.UserId == freelancerId && b.IsAccepted))
                .CountAsync();

            var activeProjects = await dbContext.Projects
                .Where(p => p.AcceptedBidId.HasValue && 
                           p.Biddings.Any(b => b.UserId == freelancerId && b.IsAccepted))
                .CountAsync();

            var freelancerDetails = new
            {
                Id = freelancer.Id,
                Name = $"{freelancer.FirstName} {freelancer.LastName}",
                Email = freelancer.Email,
                Photo = freelancer.Photo ?? "https://ik.imagekit.io/6txj3mofs/GIGHub%20(11).png?updatedAt=1750552804497",
                Role = freelancer.Role,
                
                Statistics = new
                {
                    TotalBids = freelancer.Biddings.Count,
                    AcceptedBids = freelancer.Biddings.Where(b => b.IsAccepted).Count(),
                    CompletedProjects = completedProjects,
                    ActiveProjects = activeProjects
                },
                
                Skills = freelancer.UserAccountSkills?.Select(uas => new
                {
                    Id = uas.UserSkill.Id,
                    Name = uas.UserSkill.Name
                }).Cast<object>().ToList() ?? new List<object>()
            };

            return Json(freelancerDetails);
        }

        // GET: ProjectDetails/GetProjectSummary/5
        [HttpGet]
        public async Task<IActionResult> GetProjectSummary(Guid projectId)
        {
            var project = await dbContext.Projects
                .Include(p => p.User)
                .Include(p => p.AcceptedBid)
                    .ThenInclude(ab => ab.User)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
            {
                return NotFound(new { message = "Project not found" });
            }

            var summary = new
            {
                ProjectId = project.Id,
                ProjectName = project.ProjectName,
                ClientName = $"{project.User.FirstName} {project.User.LastName}",
                FreelancerName = project.AcceptedBid?.User != null 
                    ? $"{project.AcceptedBid.User.FirstName} {project.AcceptedBid.User.LastName}" 
                    : "Not Assigned",
                ProjectStatus = project.Status,
                Budget = project.Budget,
                AcceptedBidAmount = project.AcceptedBid?.Budget.ToString() ?? "N/A",
                CreatedAt = project.CreatedAt.ToString("yyyy-MM-dd"),
                Delivery = project.AcceptedBid?.Delivery ?? "N/A"
            };

            return Json(summary);
        }

        // Helper method to parse JSON arrays stored as strings
        private List<string> ParseJsonArray(string? jsonString)
        {
            if (string.IsNullOrEmpty(jsonString))
            {
                return new List<string>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<string>>(jsonString) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}
