﻿using Microsoft.AspNetCore.Mvc;
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
    [Authorize(Roles = "Client")]
    public class ManageProjectClientController : Controller
    {
        private readonly ApplicationDbContext dbContext;
        private readonly IContractService contractService;

        public ManageProjectClientController(ApplicationDbContext context, IContractService contractService)
        {
            this.dbContext = context;
            this.contractService = contractService;
        }

        // GET: ManageProjectClient
        public async Task<IActionResult> Index()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid userId))
            {
                return Unauthorized();
            }

            // Get all projects with accepted bids (including terminated and completed)
            var ongoingProjects = await dbContext.Projects
                .Where(p => p.UserId == userId && p.AcceptedBidId != null)
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
                    
                    ClientId = project.User.Id,
                    ClientName = $"{project.User.FirstName} {project.User.LastName}",
                    ClientEmail = project.User.Email,
                    ClientPhoto = project.User.Photo ?? "https://ik.imagekit.io/6txj3mofs/GIGHub%20(11).png?updatedAt=1750552804497",
                    
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
                UserRole = "Client"
            };

            return View(viewModel);
        }

        // GET: ManageProjectClient/Details/5
        public async Task<IActionResult> Details(Guid id)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid userId))
            {
                return Unauthorized();
            }

            var project = await dbContext.Projects
                .Where(p => p.Id == id && p.UserId == userId && p.AcceptedBidId != null)
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
                
                ClientId = project.User.Id,
                ClientName = $"{project.User.FirstName} {project.User.LastName}",
                ClientEmail = project.User.Email,
                ClientUsername = project.User.UserName,
                ClientPhoto = project.User.Photo ?? "https://ik.imagekit.io/6txj3mofs/GIGHub%20(11).png?updatedAt=1750552804497",
                
                FreelancerId = freelancer.Id,
                FreelancerName = $"{freelancer.FirstName} {freelancer.LastName}",
                FreelancerEmail = freelancer.Email,
                FreelancerUsername = freelancer.UserName,
                FreelancerPhoto = freelancer.Photo ?? "https://ik.imagekit.io/6txj3mofs/GIGHub%20(11).png?updatedAt=1750552804497",
                
                AcceptedBidAmount = project.AcceptedBid.Budget,
                AcceptedBidDelivery = project.AcceptedBid.Delivery,
                AcceptedBidProposal = project.AcceptedBid.Proposal,
                BiddingAcceptedDate = project.AcceptedBid.BiddingAcceptedDate,
                
                ProjectStatus = project.Status,
                ProjectRequiredSkills = project.ProjectSkills?.Select(ps => ps.UserSkill).ToList() ?? new List<UserSkill>(),
                FreelancerSkills = freelancerSkills
            };

            // Get contract information
            var contract = await contractService.GetContractByProjectIdAsync(id);
            if (contract != null)
            {
                ViewBag.ContractId = contract.Id;
                ViewBag.ContractStatus = contract.Status;
                
                // Check if current user (client) needs to sign
                var needsSignature = !contract.ClientSignedAt.HasValue && 
                                   (contract.Status == "Draft" || contract.Status == "AwaitingClient");
                ViewBag.NeedsSignature = needsSignature;
                
                // Add status information for better UX
                ViewBag.ContractCreatedAt = contract.CreatedAt;
                ViewBag.ClientHasSigned = contract.ClientSignedAt.HasValue;
                ViewBag.FreelancerHasSigned = contract.FreelancerSignedAt.HasValue;
            }
            else
            {
                ViewBag.ContractId = null;
                ViewBag.ContractStatus = null;
                ViewBag.NeedsSignature = false;
                ViewBag.ClientHasSigned = false;
                ViewBag.FreelancerHasSigned = false;
            }

            // Pass AcceptBidId for feedback functionality
            ViewBag.AcceptBidId = project.AcceptedBidId;

            // Pass through any success/error messages from contract operations
            if (TempData["Message"] != null)
            {
                ViewBag.Message = TempData["Message"];
            }
            if (TempData["ErrorMessage"] != null)
            {
                ViewBag.ErrorMessage = TempData["ErrorMessage"];
            }

            return View(viewModel);
        }

        // POST: ManageProjectClient/MarkAsCompleted/5
        [HttpPost]
        public async Task<IActionResult> MarkAsCompleted(Guid id)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid userId))
            {
                return Unauthorized();
            }

            var project = await dbContext.Projects
                .Where(p => p.Id == id && p.UserId == userId && p.AcceptedBidId != null)
                .FirstOrDefaultAsync();

            if (project == null)
            {
                return NotFound();
            }

            project.Status = "Completed";
            await dbContext.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // POST: ManageProjectFreelancer/MarkAsCompleted/5
        [HttpPost]
        public async Task<IActionResult> MarkAsCompletedFreelancer(Guid id)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid userId))
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

        // GET: ManageProjectClient/Feedback/5
        public async Task<IActionResult> Feedback(Guid id)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid userId))
            {
                return Unauthorized();
            }

            // Get the accepted bid for this project
            var acceptedBid = await dbContext.Biddings
                .Where(b => b.Id == id && b.IsAccepted && 
                           b.Project.UserId == userId) // Ensure client owns the project
                .Include(b => b.User) // Freelancer
                .Include(b => b.Project)
                .FirstOrDefaultAsync();

            if (acceptedBid == null)
            {
                TempData["Error"] = "Invalid bid or project not found.";
                return RedirectToAction(nameof(Index));
            }

            // Check if feedback already exists
            var existingFeedback = await dbContext.FreelancerFeedbacks
                .Where(f => f.AcceptBidId == id)
                .FirstOrDefaultAsync();

            // Check if project is completed
            if (acceptedBid.Project.Status != "Completed")
            {
                TempData["Error"] = "You can only provide feedback for completed projects.";
                return RedirectToAction(nameof(Index));
            }

            var viewModel = new FeedbackViewModel
            {
                AcceptBidId = acceptedBid.Id,
                ProjectId = acceptedBid.Project.Id,
                FreelancerName = $"{acceptedBid.User.FirstName} {acceptedBid.User.LastName}",
                FreelancerPhoto = acceptedBid.User.Photo,
                ClientName = $"{User.Identity?.Name}",
                ClientPhoto = null, // Client's own photo not needed in this context
                ProjectName = acceptedBid.Project.ProjectName
            };

            // If feedback exists, populate the view model with existing data
            if (existingFeedback != null)
            {
                viewModel.Rating = existingFeedback.Rating;
                viewModel.WouldRecommend = existingFeedback.WouldRecommend;
                viewModel.Comments = existingFeedback.Comments;
                ViewBag.IsViewingExisting = true;
                ViewBag.FeedbackDate = existingFeedback.CreatedAt.ToString("MMM dd, yyyy");
            }
            else
            {
                ViewBag.IsViewingExisting = false;
            }

            return View(viewModel);
        }

        // POST: ManageProjectClient/Feedback
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Feedback(FeedbackViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid userId))
            {
                return Unauthorized();
            }

            // Validate the accepted bid belongs to a project owned by the current user
            var acceptedBid = await dbContext.Biddings
                .Where(b => b.Id == model.AcceptBidId && b.IsAccepted && 
                           b.Project.UserId == userId)
                .Include(b => b.User)
                .Include(b => b.Project)
                .FirstOrDefaultAsync();

            if (acceptedBid == null)
            {
                TempData["Error"] = "Invalid bid or unauthorized access.";
                return RedirectToAction(nameof(Index));
            }

            // Check if feedback already exists
            var existingFeedback = await dbContext.FreelancerFeedbacks
                .Where(f => f.AcceptBidId == model.AcceptBidId)
                .FirstOrDefaultAsync();

            if (existingFeedback != null)
            {
                TempData["Error"] = "You have already provided feedback for this freelancer.";
                return RedirectToAction(nameof(Index));
            }

            // Create new feedback
            var feedback = new FreelancerFeedback
            {
                Id = Guid.NewGuid(),
                AcceptBidId = model.AcceptBidId,
                FreelancerId = acceptedBid.UserId,
                Rating = model.Rating,
                WouldRecommend = model.WouldRecommend,
                Comments = model.Comments,
                CreatedAt = DateTime.UtcNow
            };

                            dbContext.FreelancerFeedbacks.Add(feedback);
                await dbContext.SaveChangesAsync();

                                 // Create notification for the freelancer
                 var notification = new Notification
                 {
                     Id = Guid.NewGuid(),
                     UserId = acceptedBid.UserId, // Freelancer's ID
                     Title = "New Feedback Received",
                     Message = $"You received feedback from {User.Identity?.Name} for the project '{acceptedBid.Project.ProjectName}'",
                     Type = "Feedback",
                     IsRead = false,
                     CreatedAt = DateTime.UtcNow,
                     RelatedUrl = $"/ManageProjectFreelancer/Feedback/{acceptedBid.Project.Id}", // Link directly to the feedback page
                     IconSvg = "<svg viewBox=\"0 0 24 24\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\"><g id=\"SVGRepo_bgCarrier\" stroke-width=\"0\"></g><g id=\"SVGRepo_tracerCarrier\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></g><g id=\"SVGRepo_iconCarrier\"> <path d=\"M11.2691 4.41115C11.5006 3.89177 11.6164 3.63208 11.7776 3.55211C11.9176 3.48263 12.082 3.48263 12.222 3.55211C12.3832 3.63208 12.499 3.89177 12.7305 4.41115L14.5745 8.54808C14.643 8.70162 14.6772 8.77839 14.7302 8.83718C14.777 8.8892 14.8343 8.93081 14.8982 8.95929C14.9705 8.99149 15.0541 9.00031 15.2213 9.01795L19.7256 9.49336C20.2911 9.55304 20.5738 9.58288 20.6997 9.71147C20.809 9.82316 20.8598 9.97956 20.837 10.1342C20.8108 10.3122 20.5996 10.5025 20.1772 10.8832L16.8125 13.9154C16.6877 14.0279 16.6252 14.0842 16.5857 14.1527C16.5507 14.2134 16.5288 14.2807 16.5215 14.3503C16.5132 14.429 16.5306 14.5112 16.5655 14.6757L17.5053 19.1064C17.6233 19.6627 17.6823 19.9408 17.5989 20.1002C17.5264 20.2388 17.3934 20.3354 17.2393 20.3615C17.0619 20.3915 16.8156 20.2495 16.323 19.9654L12.3995 17.7024C12.2539 17.6184 12.1811 17.5765 12.1037 17.56C12.0352 17.5455 11.9644 17.5455 11.8959 17.56C11.8185 17.5765 11.7457 17.6184 11.6001 17.7024L7.67662 19.9654C7.18404 20.2495 6.93775 20.3915 6.76034 20.3615C6.60623 20.3354 6.47319 20.2388 6.40075 20.1002C6.31736 19.9408 6.37635 19.6627 6.49434 19.1064L7.4341 14.6757C7.46898 14.5112 7.48642 14.429 7.47814 14.3503C7.47081 14.2807 7.44894 14.2134 7.41394 14.1527C7.37439 14.0842 7.31195 14.0279 7.18708 13.9154L3.82246 10.8832C3.40005 10.5025 3.18884 10.3122 3.16258 10.1342C3.13978 9.97956 3.19059 9.82316 3.29993 9.71147C3.42581 9.58288 3.70856 9.55304 4.27406 9.49336L8.77835 9.01795C8.94553 9.00031 9.02911 8.99149 9.10139 8.95929C9.16534 8.93081 9.2226 8.8892 9.26946 8.83718C9.32241 8.77839 9.35663 8.70162 9.42508 8.54808L11.2691 4.41115Z\" stroke=\"#000000\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></path> </g></svg>"
                 };

                dbContext.Notifications.Add(notification);
                await dbContext.SaveChangesAsync();

            TempData["Message"] = "Thank you for your feedback! It has been submitted successfully.";
            return RedirectToAction(nameof(Details), new { id = acceptedBid.Project.Id });
        }
    }
}
