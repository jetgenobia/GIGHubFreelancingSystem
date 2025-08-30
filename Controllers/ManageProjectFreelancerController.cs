using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Freelancing.Data;
using Freelancing.Models;
using Freelancing.Models.Entities;
using Freelancing.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;

namespace Freelancing.Controllers
{
    [Authorize(Roles = "Freelancer")]
    public class ManageProjectFreelancerController : Controller
    {
        private readonly ApplicationDbContext dbContext;
        private readonly IContractService contractService;

        public ManageProjectFreelancerController(ApplicationDbContext context, IContractService contractService)
        {
            this.dbContext = context;
            this.contractService = contractService;
        }

        // GET: ManageProjectFreelancer
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // Get all projects where freelancer's bid was accepted (including terminated and completed)
            var ongoingProjects = await dbContext.Projects
                .Where(p => p.AcceptedBidId != null && 
                           p.Biddings.Any(b => b.UserId == userId && b.IsAccepted))
                .Include(p => p.User) // Client
                .Include(p => p.AcceptedBid)
                    .ThenInclude(ab => ab.User) // Freelancer
                .Include(p => p.ProjectSkills)
                    .ThenInclude(ps => ps.UserSkill)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var projectViewModels = new List<OngoingProjectViewModel>();

            foreach (var project in ongoingProjects)
            {
                var client = project.User;
                var freelancer = project.AcceptedBid.User;
                
                // Get freelancer skills
                var freelancerSkills = await dbContext.UserAccountSkills
                    .Where(uas => uas.UserAccountId == freelancer.Id)
                    .Include(uas => uas.UserSkill)
                    .Select(uas => uas.UserSkill)
                    .ToListAsync();

                // Get contract status for this project
                var contract = await contractService.GetContractByProjectIdAsync(project.Id);
                var projectStatus = contract?.Status ?? GetProjectStatus(project);

                var ongoingProjectViewModel = new OngoingProjectViewModel
                {
                    ProjectId = project.Id,
                    ProjectName = project.ProjectName,
                    ProjectDescription = project.ProjectDescription,
                    ProjectBudget = project.Budget,
                    ProjectCategory = project.Category,
                    ProjectCreatedAt = project.CreatedAt,
                    ProjectImageUrls = ParseJsonArray(project.ImagePaths),
                    
                    ClientId = client.Id,
                    ClientName = $"{client.FirstName} {client.LastName}",
                    ClientEmail = client.Email,
                    ClientPhoto = client.Photo ?? "https://ik.imagekit.io/6txj3mofs/GIGHub%20(11).png?updatedAt=1750552804497",
                    
                    FreelancerId = freelancer.Id,
                    FreelancerName = $"{freelancer.FirstName} {freelancer.LastName}",
                    FreelancerEmail = freelancer.Email,
                    FreelancerPhoto = freelancer.Photo ?? "https://ik.imagekit.io/6txj3mofs/GIGHub%20(11).png?updatedAt=1750552804497",
                    
                    AcceptedBidAmount = project.AcceptedBid.Budget,
                    AcceptedBidDelivery = project.AcceptedBid.Delivery,
                                    AcceptedBidProposal = project.AcceptedBid.Proposal,
                BiddingAcceptedDate = project.AcceptedBid.BiddingAcceptedDate,
                
                ProjectStatus = projectStatus,
                    ProjectRequiredSkills = project.ProjectSkills?.Select(ps => ps.UserSkill).ToList() ?? new List<UserSkill>(),
                    FreelancerSkills = freelancerSkills
                };

                projectViewModels.Add(ongoingProjectViewModel);
            }

            var viewModel = new OngoingProjectListViewModel
            {
                OngoingProjects = projectViewModels,
                TotalActiveProjects = projectViewModels.Count,
                UserRole = "Freelancer"
            };

            return View(viewModel);
        }

        // GET: ManageProjectFreelancer/Details/5
        public async Task<IActionResult> Details(Guid id)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var project = await dbContext.Projects
                .Where(p => p.Id == id && p.AcceptedBidId != null && 
                           p.Biddings.Any(b => b.UserId == userId && b.IsAccepted))
                .Include(p => p.User)
                .Include(p => p.AcceptedBid)
                    .ThenInclude(ab => ab.User)
                .Include(p => p.ProjectSkills)
                    .ThenInclude(ps => ps.UserSkill)
                .FirstOrDefaultAsync();

            if (project == null)
            {
                return NotFound();
            }

            var client = project.User;
            var freelancer = project.AcceptedBid.User;
            
            // Get freelancer skills
            var freelancerSkills = await dbContext.UserAccountSkills
                .Where(uas => uas.UserAccountId == freelancer.Id)
                .Include(uas => uas.UserSkill)
                .Select(uas => uas.UserSkill)
                .ToListAsync();

            var viewModel = new OngoingProjectViewModel
            {
                ProjectId = project.Id,
                ProjectName = project.ProjectName,
                ProjectDescription = project.ProjectDescription,
                ProjectBudget = project.Budget,
                ProjectCategory = project.Category,
                ProjectCreatedAt = project.CreatedAt,
                ProjectImageUrls = ParseJsonArray(project.ImagePaths),
                
                ClientId = client.Id,
                ClientName = $"{client.FirstName} {client.LastName}",
                ClientEmail = client.Email,
                ClientUsername = client.UserName,
                ClientPhoto = client.Photo ?? "https://ik.imagekit.io/6txj3mofs/GIGHub%20(11).png?updatedAt=1750552804497",
                
                FreelancerId = freelancer.Id,
                FreelancerName = $"{freelancer.FirstName} {freelancer.LastName}",
                FreelancerEmail = freelancer.Email,
                FreelancerUsername = freelancer.UserName,
                FreelancerPhoto = freelancer.Photo ?? "https://ik.imagekit.io/6txj3mofs/GIGHub%20(11).png?updatedAt=1750552804497",
                
                AcceptedBidAmount = project.AcceptedBid.Budget,
                AcceptedBidDelivery = project.AcceptedBid.Delivery,
                AcceptedBidProposal = project.AcceptedBid.Proposal,
                BiddingAcceptedDate = project.AcceptedBid.BiddingAcceptedDate,
                
                ProjectStatus = GetProjectStatus(project),
                ProjectRequiredSkills = project.ProjectSkills?.Select(ps => ps.UserSkill).ToList() ?? new List<UserSkill>(),
                FreelancerSkills = freelancerSkills
            };

            // Get contract information
            var contract = await contractService.GetContractByProjectIdAsync(id);
            if (contract != null)
            {
                ViewBag.ContractId = contract.Id;
                ViewBag.ContractStatus = contract.Status;
                
                // Check if current user (freelancer) needs to sign
                var needsSignature = !contract.FreelancerSignedAt.HasValue && 
                                   (contract.Status == "AwaitingFreelancer" || 
                                    contract.Status == "Draft");
                ViewBag.NeedsSignature = needsSignature;
            }
            else
            {
                ViewBag.ContractId = null;
                ViewBag.ContractStatus = null;
                ViewBag.NeedsSignature = false;
            }

            // Get feedback information for this project
            var feedback = await dbContext.FreelancerFeedbacks
                .Where(f => f.AcceptBidId == project.AcceptedBidId)
                .FirstOrDefaultAsync();

            if (feedback != null)
            {
                ViewBag.HasFeedback = true;
                ViewBag.FeedbackRating = feedback.Rating;
                ViewBag.FeedbackRecommendation = feedback.WouldRecommend;
                ViewBag.FeedbackComments = feedback.Comments;
                ViewBag.FeedbackDate = feedback.CreatedAt;
            }
            else
            {
                ViewBag.HasFeedback = false;
            }

            return View(viewModel);
        }

        // POST: ManageProjectFreelancer/MarkAsCompleted/5
        [HttpPost]
        public async Task<IActionResult> MarkAsCompleted(Guid id)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var project = await dbContext.Projects
                .Where(p => p.Id == id && p.AcceptedBidId != null && 
                           p.Biddings.Any(b => b.UserId == userId && b.IsAccepted))
                .FirstOrDefaultAsync();

            if (project == null)
            {
                return NotFound();
            }

            project.Status = "Completed";
            await dbContext.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // Helper method to determine project status
        private string GetProjectStatus(Project project)
        {
            // If project has a specific status set, use that
            if (!string.IsNullOrEmpty(project.Status))
            {
                return project.Status;
            }
            
            // Otherwise, determine status based on accepted bid
            return project.AcceptedBidId.HasValue ? "Active" : "Open";
        }

        // GET: ManageProjectFreelancer/Feedback/5
        public async Task<IActionResult> Feedback(Guid id)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

                                 // Get the project where freelancer's bid was accepted
                     var project = await dbContext.Projects
                         .Where(p => p.Id == id && p.AcceptedBidId != null && 
                                    p.Biddings.Any(b => b.UserId == userId && b.IsAccepted))
                         .Include(p => p.User) // Client
                         .Include(p => p.AcceptedBid)
                         .ThenInclude(ab => ab.User) // Freelancer
                         .FirstOrDefaultAsync();

            if (project == null)
            {
                TempData["Error"] = "Project not found or unauthorized access.";
                return RedirectToAction(nameof(Index));
            }

            // Get feedback for this project
            var feedback = await dbContext.FreelancerFeedbacks
                .Where(f => f.AcceptBidId == project.AcceptedBidId)
                .FirstOrDefaultAsync();

            if (feedback == null)
            {
                TempData["Error"] = "No feedback found for this project.";
                return RedirectToAction(nameof(Details), new { id = project.Id });
            }

            var viewModel = new FeedbackViewModel
            {
                AcceptBidId = project.AcceptedBidId.Value,
                ProjectId = project.Id,
                FreelancerName = $"{project.AcceptedBid.User.FirstName} {project.AcceptedBid.User.LastName}",
                FreelancerPhoto = project.AcceptedBid.User.Photo,
                ClientName = $"{project.User.FirstName} {project.User.LastName}",
                ClientPhoto = project.User.Photo,
                ProjectName = project.ProjectName,
                Rating = feedback.Rating,
                WouldRecommend = feedback.WouldRecommend,
                Comments = feedback.Comments
            };

            ViewBag.IsViewingExisting = true;
            ViewBag.FeedbackDate = feedback.CreatedAt.ToString("MMM dd, yyyy");

            return View(viewModel);
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
