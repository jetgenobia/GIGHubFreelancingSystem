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
    [Authorize(Roles = "Client")]
    public class ManageProjectClientController : Controller
    {
        private readonly ApplicationDbContext dbContext;
        private readonly IContractService contractService;
        private readonly ISmartHiringService smartHiringService;
        private readonly INotificationService notificationService;

        public ManageProjectClientController(
            ApplicationDbContext context, 
            IContractService contractService,
            ISmartHiringService smartHiringService,
            INotificationService notificationService)
        {
            this.dbContext = context;
            this.contractService = contractService;
            this.smartHiringService = smartHiringService;
            this.notificationService = notificationService;
        }

        // GET: ManageProjectClient
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
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
                    ProjectDeadline = project.Deadline,

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

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
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
                ProjectDeadline = project.Deadline,

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

                if (!string.IsNullOrEmpty(contract.Timeline))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(contract.Timeline);
                        var root = doc.RootElement;

                        if (root.ValueKind == JsonValueKind.Object)
                        {
                            if (root.TryGetProperty("startDate", out var startEl) && startEl.ValueKind == JsonValueKind.String)
                            {
                                var startStr = startEl.GetString();
                                if (!string.IsNullOrEmpty(startStr) && DateTime.TryParse(startStr, out var startDt))
                                {
                                    viewModel.ContractStartDate = startDt;
                                }
                            }

                            if (root.TryGetProperty("deadline", out var deadlineEl) && deadlineEl.ValueKind == JsonValueKind.String)
                            {
                                var deadlineStr = deadlineEl.GetString();
                                if (!string.IsNullOrEmpty(deadlineStr) && DateTime.TryParse(deadlineStr, out var deadlineDt))
                                {
                                    viewModel.ContractDeadline = deadlineDt;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // If parsing fails, silently ignore and fall back to project/bid dates.
                    }
                }
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

            // Check mentorship completion status for freelancer
            var completedAsMentor = await dbContext.MentorshipMatches
                .AnyAsync(mm => mm.MentorId == freelancer.Id && mm.Status == "Completed");

            var completedAsMentee = await dbContext.MentorshipMatches
                .AnyAsync(mm => mm.MenteeId == freelancer.Id && mm.Status == "Completed");

            ViewBag.MentorshipData = (completedAsMentor, completedAsMentee);

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
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
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
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
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
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
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

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
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

        // GET: ManageProjectClient/SmartHiring/5
        public async Task<IActionResult> SmartHiring(Guid id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // Get project details
            var project = await dbContext.Projects
                .Where(p => p.Id == id && p.UserId == userId)
                .Include(p => p.Biddings)
                    .ThenInclude(b => b.User)
                .Include(p => p.ProjectSkills)
                    .ThenInclude(ps => ps.UserSkill)
                .FirstOrDefaultAsync();

            if (project == null)
            {
                return NotFound();
            }

            // Check if project has biddings
            if (project.Biddings?.Any() != true)
            {
                TempData["Info"] = "No bids have been submitted for this project yet.";
                return RedirectToAction("Details", "ProjectDetails", new { id = id });
            }

            try
            {
                // Get smart hiring recommendations
                var recommendations = await smartHiringService.GetBestFreelancersAsync(id);
                var insights = await smartHiringService.GetProjectInsightsAsync(id);

                var viewModel = new SmartHiringViewModel
                {
                    Project = project,
                    Recommendations = recommendations,
                    ProjectInsights = insights,
                    TopRecommendations = recommendations.Take(1).ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Unable to generate smart hiring recommendations at this time.";
                return RedirectToAction("Details", "ProjectDetails", new { id = id });
            }
        }

        // GET: ManageProjectClient/BidderDetails/5?freelancerId=guid
        public async Task<IActionResult> BidderDetails(Guid id, string freelancerId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // Verify project ownership
            var project = await dbContext.Projects
                .Where(p => p.Id == id && p.UserId == userId)
                .Include(p => p.ProjectSkills)
                    .ThenInclude(ps => ps.UserSkill)
                .FirstOrDefaultAsync();

            if (project == null)
            {
                return NotFound();
            }

            // Get bidding details
            var bid = await dbContext.Biddings
                .Where(b => b.ProjectId == id && b.UserId == freelancerId)
                .Include(b => b.User)
                    .ThenInclude(u => u.UserAccountSkills)
                        .ThenInclude(uas => uas.UserSkill)
                .FirstOrDefaultAsync();

            if (bid == null)
            {
                return NotFound("Bid not found");
            }

            try
            {
                // Get detailed prediction for this freelancer
                var prediction = await smartHiringService.GetFreelancerScoreAsync(id, freelancerId);

                // Get freelancer's past feedback
                var pastFeedbacks = await dbContext.FreelancerFeedbacks
                    .Where(f => f.FreelancerId == freelancerId)
                    .OrderByDescending(f => f.CreatedAt)
                    .Take(5)
                    .ToListAsync();

                // Get freelancer's completed projects count
                var completedProjects = await dbContext.Biddings
                    .Where(b => b.UserId == freelancerId && b.IsAccepted && b.Project.Status == "Completed")
                    .CountAsync();

                var viewModel = new BidderDetailsViewModel
                {
                    Project = project,
                    Bid = bid,
                    Freelancer = bid.User,
                    Prediction = prediction,
                    PastFeedbacks = pastFeedbacks,
                    CompletedProjectsCount = completedProjects,
                    FreelancerSkills = bid.User.UserAccountSkills?.Select(uas => uas.UserSkill).ToList() ?? new List<UserSkill>()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Unable to load detailed analysis for this bidder.";
                return RedirectToAction(nameof(SmartHiring), new { id = id });
            }
        }

        // POST: ManageProjectClient/AcceptBid/5
        [HttpPost]
        public async Task<IActionResult> AcceptBid(Guid bidId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var bid = await dbContext.Biddings
                .Where(b => b.Id == bidId && b.Project.UserId == userId)
                .Include(b => b.Project)
                .Include(b => b.User)
                .FirstOrDefaultAsync();

            if (bid == null)
            {
                return NotFound();
            }

            try
            {
                // Update project and bid
                bid.IsAccepted = true;
                bid.BiddingAcceptedDate = DateTime.UtcNow;
                bid.Project.AcceptedBidId = bidId;
                bid.Project.Status = "Active";

                // Create notification for freelancer
                var notification = new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = bid.UserId,
                    Title = "🎉 Congratulations! Your bid was accepted",
                    Message = $"Your bid for '{bid.Project.ProjectName}' has been accepted by {User.Identity?.Name}. You can now start working on the project.",
                    Type = "BidAcceptance",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    RelatedUrl = $"/ManageProjectFreelancer/Details/{bid.Project.Id}",
                    IconSvg = "<svg viewBox=\"0 0 24 24\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M12 2L15.09 8.26L22 9L17 14L18.18 21L12 17.77L5.82 21L7 14L2 9L8.91 8.26L12 2Z\" fill=\"#FFD700\" stroke=\"#FFD700\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/></svg>"
                };

                dbContext.Notifications.Add(notification);
                await dbContext.SaveChangesAsync();

                // Record hiring outcome for ML learning (initially neutral, will be updated based on feedback)
                await smartHiringService.RecordHiringOutcomeAsync(bid.Project.Id, bid.UserId, true);

                TempData["Message"] = $"Successfully accepted bid from {bid.User.FirstName} {bid.User.LastName}!";
                return RedirectToAction(nameof(Details), new { id = bid.Project.Id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to accept the bid. Please try again.";
                return RedirectToAction(nameof(SmartHiring), new { id = bid.Project.Id });
            }
        }

        // GET: ManageProjectClient/ExportTrainingData
        [HttpGet]
        public async Task<IActionResult> ExportTrainingData()
        {
            try
            {
                var tempFilePath = Path.GetTempFileName();
                tempFilePath = Path.ChangeExtension(tempFilePath, ".csv");

                // Get the feature service and export training data
                var featureService = HttpContext.RequestServices.GetService<ISmartHiringFeatureService>();
                if (featureService == null)
                {
                    TempData["Error"] = "Smart hiring service not available.";
                    return RedirectToAction(nameof(Index));
                }

                await featureService.ExportTrainingDataToCsvAsync(tempFilePath);

                var fileBytes = await System.IO.File.ReadAllBytesAsync(tempFilePath);
                System.IO.File.Delete(tempFilePath);

                return File(fileBytes, "text/csv", $"smart_hiring_training_data_{DateTime.Now:yyyyMMdd}.csv");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to export training data: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetDeadline(Guid projectId, DateTime deadline)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "User not authenticated.";
                return RedirectToAction(nameof(Details), new { id = projectId });
            }

            try
            {
                // Get the project and verify ownership
                var project = await dbContext.Projects
                    .Where(p => p.Id == projectId && p.UserId == userId && p.AcceptedBidId != null)
                    .Include(p => p.AcceptedBid)
                    .FirstOrDefaultAsync();

                if (project == null)
                {
                    TempData["ErrorMessage"] = "Project not found or unauthorized access.";
                    return RedirectToAction(nameof(Details), new { id = projectId });
                }

                // Check if there's an active contract
                var contract = await contractService.GetContractByProjectIdAsync(projectId);
                if (contract == null || contract.Status != "Active")
                {
                    TempData["ErrorMessage"] = "Cannot set deadline without an active contract.";
                    return RedirectToAction(nameof(Details), new { id = projectId });
                }

                // Validate deadline is in the future
                if (deadline <= DateTime.Now)
                {
                    TempData["ErrorMessage"] = "Deadline must be set in the future.";
                    return RedirectToAction(nameof(Details), new { id = projectId });
                }

                // Update the project deadline
                project.Deadline = deadline;
                await dbContext.SaveChangesAsync();

                // Use the NotificationService with the new SVG icon
                await notificationService.CreateNotificationAsync(
                    project.AcceptedBid.UserId,
                    "New Project Deadline Set",
                    $"A deadline has been set for project '{project.ProjectName}' - Due: {deadline:MMM dd, yyyy 'at' h:mm tt}",
                    "Deadline",
                    "<svg viewBox=\"0 0 24 24\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\"><g id=\"SVGRepo_bgCarrier\" stroke-width=\"0\"></g><g id=\"SVGRepo_tracerCarrier\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></g><g id=\"SVGRepo_iconCarrier\"> <path d=\"M3 9H21M12 18V12M15 15.001L9 15M7 3V5M17 3V5M6.2 21H17.8C18.9201 21 19.4802 21 19.908 20.782C20.2843 20.5903 20.5903 20.2843 20.782 19.908C21 19.4802 21 18.9201 21 17.8V8.2C21 7.07989 21 6.51984 20.782 6.09202C20.5903 5.71569 20.2843 5.40973 19.908 5.21799C19.4802 5 18.9201 5 17.8 5H6.2C5.0799 5 4.51984 5 4.09202 5.21799C3.71569 5.40973 3.40973 5.71569 3.21799 6.09202C3 6.51984 3 7.07989 3 8.2V17.8C3 18.9201 3 19.4802 3.21799 19.908C3.40973 20.2843 3.71569 20.5903 4.09202 20.782C4.51984 21 5.07989 21 6.2 21Z\" stroke=\"#000000\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></path> </g></svg>",
                    $"/ManageProjectFreelancer/Details/{project.Id}"
                );

                TempData["Message"] = "Deadline set successfully!";
                return RedirectToAction(nameof(Details), new { id = projectId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while setting the deadline.";
                return RedirectToAction(nameof(Details), new { id = projectId });
            }
        }
    }
}
