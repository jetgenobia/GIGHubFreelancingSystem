using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Freelancing.Data;
using Freelancing.Models;
using Freelancing.Models.Entities;
using Freelancing.Services;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;

namespace Freelancing.Controllers
{
    // Handles client-specific functionalities such as managing projects, viewing bids, and accepting bids.
    [Authorize(Roles = "Client")]
    public class ClientController : Controller
    {
        private readonly ApplicationDbContext dbContext;
        private readonly INotificationService notificationService;
        private readonly UserManager<UserAccount> _userManager;
        private readonly SignInManager<UserAccount> _signInManager;
        private readonly IEmailService _emailService;
        private readonly ISmartHiringService _smartHiringService;

        public ClientController(ApplicationDbContext context, INotificationService notificationService, UserManager<UserAccount> userManager, SignInManager<UserAccount> signInManager, IEmailService emailService, ISmartHiringService smartHiringService)
        {
            this.dbContext = context;
            this.notificationService = notificationService;
            this._userManager = userManager;
            this._signInManager = signInManager;
            this._emailService = emailService;
            this._smartHiringService = smartHiringService;
        }

        // Helper method to generate unique filename while preserving original name
        private string GenerateUniqueFileName(string originalFileName, string uploadsFolder)
        {
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalFileName);
            var fileExtension = Path.GetExtension(originalFileName);
            var uniqueFileName = originalFileName;
            var counter = 1;

            // Keep trying until we find a unique filename
            while (System.IO.File.Exists(Path.Combine(uploadsFolder, uniqueFileName)))
            {
                uniqueFileName = $"{fileNameWithoutExtension}_{counter}{fileExtension}";
                counter++;
            }

            return uniqueFileName;
        }
        // Displays the client dashboard with project statistics and a list of projects.
        public async Task<IActionResult> Dashboard(string message = null)
        {
            // Get the user ID from the claims to filter projects by the logged-in user.
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Fetch projects associated with the logged-in user, including accepted bids.
            var projects = await dbContext.Projects
                .Include(p => p.AcceptedBid)
                .ThenInclude(b => b.User)
                .Where(p => p.UserId == userId)
                .ToListAsync();

            // Sum budgets of accepted bids only for projects with Status == "Completed"
            var totalAcceptedBudgetForCompleted = await dbContext.Biddings
                .Where(b => b.Project.UserId == userId && b.IsAccepted && b.Project.Status == "Completed")
                .SumAsync(b => (int?)b.Budget) ?? 0;

            ViewBag.TotalAcceptedBudget = totalAcceptedBudgetForCompleted;

            var biddings = await dbContext.Biddings
                .Where(b => b.Project.UserId == userId)
                .ToListAsync();

            var terminatedContracts = await dbContext.Contracts
                .Include(c => c.Project)
                .Where(c => c.Project != null && c.Project.UserId == userId && c.Status == "Terminated")
                .ToListAsync();

            var terminatedProjectIds = terminatedContracts.Select(c => c.ProjectId).ToHashSet();

            ViewBag.TerminatedProjectIds = terminatedProjectIds;

            var feedbacks = await dbContext.FreelancerFeedbacks
                .Where(f => f.AcceptBidding != null && f.AcceptBidding.Project.UserId == userId)
                .Include(f => f.Freelancer)
                .Include(f => f.AcceptBidding)
                    .ThenInclude(b => b.Project)
                .Where(f => f.AcceptBidding != null && f.AcceptBidding.Project.UserId == userId)
                .ToListAsync();

            // Create a view model to hold project statistics and the list of projects.
            var viewModel = new ClientDashboard
            {
                Projects = projects,
                TotalProjects = projects.Count,
                OpenProjects = projects.Count(p => !p.AcceptedBidId.HasValue),
                ClosedProjects = projects.Count(p => p.AcceptedBidId.HasValue),
                freelancerFeedbacks = feedbacks
            };

            if (!string.IsNullOrEmpty(message))
            {
                ViewBag.Message = message;
            }

            return View(viewModel);
        }

        public async Task<IActionResult> ActiveProjects(string message = null)
        {
            // Get the user ID from the claims to filter projects by the logged-in user.
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Fetch projects associated with the logged-in user, including accepted bids.
            var projects = await dbContext.Projects
                .Include(p => p.AcceptedBid)
                .ThenInclude(b => b.User)
                .Where(p => p.UserId == userId)
                .ToListAsync();

            // Sum budgets of accepted bids only for projects with Status == "Completed"
            var totalAcceptedBudgetForCompleted = await dbContext.Biddings
                .Where(b => b.Project.UserId == userId && b.IsAccepted && b.Project.Status == "Completed")
                .SumAsync(b => (int?)b.Budget) ?? 0;

            ViewBag.TotalAcceptedBudget = totalAcceptedBudgetForCompleted;

            var biddings = await dbContext.Biddings
                .Where(b => b.Project.UserId == userId)
                .ToListAsync();

            var terminatedContracts = await dbContext.Contracts
                .Include(c => c.Project)
                .Where(c => c.Project != null && c.Project.UserId == userId && c.Status == "Terminated")
                .ToListAsync();

            var terminatedProjectIds = terminatedContracts.Select(c => c.ProjectId).ToHashSet();

            ViewBag.TerminatedProjectIds = terminatedProjectIds;

            var feedbacks = await dbContext.FreelancerFeedbacks
                .Where(f => f.AcceptBidding != null && f.AcceptBidding.Project.UserId == userId)
                .Include(f => f.Freelancer)
                .Include(f => f.AcceptBidding)
                    .ThenInclude(b => b.Project)
                .Where(f => f.AcceptBidding != null && f.AcceptBidding.Project.UserId == userId)
                .ToListAsync();

            // Create a view model to hold project statistics and the list of projects.
            var viewModel = new ClientDashboard
            {
                Projects = projects,
                TotalProjects = projects.Count,
                OpenProjects = projects.Count(p => !p.AcceptedBidId.HasValue),
                ClosedProjects = projects.Count(p => p.AcceptedBidId.HasValue),
                freelancerFeedbacks = feedbacks
            };

            if (!string.IsNullOrEmpty(message))
            {
                ViewBag.Message = message;
            }

            return View(viewModel);
        }

        public async Task<IActionResult> Biddings()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var projects = await dbContext.Projects
                .Include(p => p.AcceptedBid)
                .Where(p => p.UserId == userId)
                .ToListAsync();

            var biddings = await dbContext.Biddings
                .Where(b => b.Project.UserId == userId)
                .ToListAsync();

            var viewModel = new Biddings
            {
                Projects = projects,
                OpenProjects = projects.Count(p => !p.AcceptedBidId.HasValue),
                ClosedProjects = projects.Count(p => p.AcceptedBidId.HasValue),
            };

            return View(viewModel);
        }

        public async Task<IActionResult> Postings()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var projects = await dbContext.Projects
                .Include(p => p.AcceptedBid)
                .Where(p => p.UserId == userId)
                .ToListAsync();

            var biddings = await dbContext.Biddings
                .Where(b => b.Project.UserId == userId)
                .ToListAsync();

            var viewModel = new Postings
            {
                Projects = projects,
                OpenProjects = projects.Count(p => !p.AcceptedBidId.HasValue),
                ClosedProjects = projects.Count(p => p.AcceptedBidId.HasValue),
            };

            return View(viewModel);
        }

        public async Task<IActionResult> Feedbacks()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var feedbacks = await dbContext.FreelancerFeedbacks
                .Where(f => f.AcceptBidding != null && f.AcceptBidding.Project.UserId == userId)
                .Include(f => f.Freelancer)
                .Include(f => f.AcceptBidding)
                    .ThenInclude(b => b.Project)
                .Where(f => f.AcceptBidding != null && f.AcceptBidding.Project.UserId == userId)
                .ToListAsync();

            var viewModel = new Feedbacks
            {
                freelancerFeedbacks = feedbacks
            };

            return View(viewModel);
        }

        // Displays the feed of all projects available for bidding.
        [HttpGet]
        public async Task<IActionResult> Feed()
        {
            var project = await dbContext.Projects
                .Include(p => p.User)
                .Include(p => p.ProjectSkills)
                .ThenInclude(ps => ps.UserSkill)
                .ToListAsync();
            return View(project);
        }
        [HttpGet]
        public async Task<IActionResult> Project(Guid id)
        {
            var projects = await dbContext.Projects
                .Include(p => p.User)
                .Include(p => p.Biddings)
                .ThenInclude(b => b.User)
                .ThenInclude(u => u.UserAccountSkills)
                .ThenInclude(uas => uas.UserSkill)
                .Include(p => p.ProjectSkills)
                .ThenInclude(ps => ps.UserSkill)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projects == null)
                return NotFound();

            // Add mentorship completion data for each bidder
            if (projects.Biddings != null)
            {
                foreach (var bid in projects.Biddings)
                {
                    if (bid.User != null)
                    {
                        // Check if user has completed mentorship as mentor
                        var completedAsMentor = await dbContext.MentorshipMatches
                            .AnyAsync(mm => mm.MentorId == bid.User.Id && mm.Status == "Completed");

                        // Check if user has completed mentorship as mentee
                        var completedAsMentee = await dbContext.MentorshipMatches
                            .AnyAsync(mm => mm.MenteeId == bid.User.Id && mm.Status == "Completed");

                        // Store this data in ViewBag for the view to access
                        if (ViewBag.MentorshipData == null)
                            ViewBag.MentorshipData = new Dictionary<string, (bool, bool)>();

                        ViewBag.MentorshipData[bid.User.Id] = (completedAsMentor, completedAsMentee);
                    }
                }
            }

            return View(projects);
        }
        // Displays the form to create a new project.
        [HttpGet]
        public IActionResult Post()
        {
            return View();
        }

        // Gets skills by category for the skills modal
        [HttpGet]
        public async Task<IActionResult> GetSkillsByCategory(string category)
        {
            var skills = await dbContext.UserSkills
                .Where(s => string.IsNullOrEmpty(category) || s.Category == category)
                .OrderBy(s => s.Name)
                .ToListAsync();

            return Json(skills);
        }

        // Handles the submission of the project creation form, validating the input and saving the project to the database.
        [HttpPost]
        public async Task<IActionResult> Post(AddProject viewModel)
        {
            // Debug: Log the incoming data
            System.Diagnostics.Debug.WriteLine($"Post method called");
            System.Diagnostics.Debug.WriteLine($"SelectedSkillIds count: {viewModel.SelectedSkillIds?.Count ?? 0}");
            if (viewModel.SelectedSkillIds != null)
            {
                foreach (var skillId in viewModel.SelectedSkillIds)
                {
                    System.Diagnostics.Debug.WriteLine($"Skill ID: {skillId}");
                }
            }
            
            if (ModelState.IsValid)
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    List<string> imagePaths = new List<string>();
                    
                    // Handle multiple file uploads
                    if (viewModel.ProjectImages != null && viewModel.ProjectImages.Any())
                    {
                        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".svg" };
                        
                        foreach (var file in viewModel.ProjectImages)
                        {
                            if (file != null && file.Length > 0)
                            {
                                // Validate file type
                                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                                
                                if (!allowedExtensions.Contains(fileExtension))
                                {
                                    ModelState.AddModelError("ProjectImages", $"File {file.FileName} is not a valid image type. Only JPG, PNG, GIF, and SVG files are allowed.");
                                    return View(viewModel);
                                }
                                
                                // Validate file size (max 10MB)
                                if (file.Length > 10 * 1024 * 1024)
                                {
                                    ModelState.AddModelError("ProjectImages", $"File {file.FileName} is too large. File size must be less than 10MB.");
                                    return View(viewModel);
                                }
                            }
                        }
                        
                        // Create project post uploads directory if it doesn't exist
                        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "projectpost");
                        if (!Directory.Exists(uploadsDir))
                        {
                            Directory.CreateDirectory(uploadsDir);
                        }
                        
                        // Process each file
                        foreach (var file in viewModel.ProjectImages)
                        {
                            if (file != null && file.Length > 0)
                            {
                                var fileName = GenerateUniqueFileName(file.FileName, uploadsDir);
                                var filePath = Path.Combine(uploadsDir, fileName);
                                
                                // Save file
                                using (var stream = new FileStream(filePath, FileMode.Create))
                                {
                                    await file.CopyToAsync(stream);
                                }
                                
                                imagePaths.Add($"/uploads/projectpost/{fileName}");
                            }
                        }
                    }
                    
                    var project = new Project
                    {
                        UserId = userId,
                        ProjectName = viewModel.ProjectName,
                        ProjectDescription = viewModel.ProjectDescription,
                        Budget = viewModel.Budget,
                        Category = viewModel.Category,
                        ImagePaths = imagePaths.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(imagePaths) : null,
                        CreatedAt = DateTime.UtcNow.ToLocalTime()
                    };
                    await dbContext.Projects.AddAsync(project);
                    await dbContext.SaveChangesAsync();

                    // Add selected skills to the project
                    if (viewModel.SelectedSkillIds != null && viewModel.SelectedSkillIds.Any())
                    {
                        var projectSkills = viewModel.SelectedSkillIds.Select(skillId => new ProjectSkill
                        {
                            ProjectId = project.Id,
                            UserSkillId = skillId
                        }).ToList();

                        await dbContext.ProjectSkills.AddRangeAsync(projectSkills);
                        await dbContext.SaveChangesAsync();
                        
                        // Log for debugging
                        System.Diagnostics.Debug.WriteLine($"Added {projectSkills.Count} skills to project {project.Id}");
                    }
                    else
                    {
                        // Log for debugging
                        System.Diagnostics.Debug.WriteLine("No skills selected or SelectedSkillIds is null/empty");
                    }

                    ModelState.Clear();

                    return RedirectToAction("Dashboard", "Client", new { message = "Project posted successfully!" });
                }
                else
                {
                    ModelState.AddModelError("", "Unable to determine the logged-in user.");
                }
            }
            return View(viewModel);
        }
        // Displays the form to edit an existing project.
        [HttpGet]
        public async Task<IActionResult> EditPost(Guid id, string message = null)
        {
            var project = await dbContext.Projects
                .Include(p => p.ProjectSkills)
                .ThenInclude(ps => ps.UserSkill)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
                return NotFound();

            if (!string.IsNullOrEmpty(message))
            {
                ViewBag.Message = message;
            }

            return View(project);
        }
        // Handles the submission of the project editing form, allowing users to save changes or delete the project.
        [HttpPost]
        public async Task<IActionResult> EditPost(Project viewModel, string action, List<Guid> SelectedSkillIds, List<IFormFile> ProjectImages, List<string> ExistingImagePaths)
        {
            var project = await dbContext.Projects
                .Include(p => p.ProjectSkills)
                .FirstOrDefaultAsync(p => p.Id == viewModel.Id);

            if (project == null)
                return RedirectToAction("Dashboard", "Client");

            if (action == "save")
            {
                project.ProjectName = viewModel.ProjectName;
                project.ProjectDescription = viewModel.ProjectDescription;
                project.Budget = viewModel.Budget;
                project.Category = viewModel.Category;

                // Handle images (existing + new)
                List<string> allImagePaths = new List<string>();

                // Add existing images that weren't removed
                if (ExistingImagePaths != null && ExistingImagePaths.Any())
                {
                    allImagePaths.AddRange(ExistingImagePaths);
                }

                // Handle new images if uploaded
                if (ProjectImages != null && ProjectImages.Any())
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".svg" };

                    // Create project post uploads directory if it doesn't exist
                    var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "projectpost");
                    if (!Directory.Exists(uploadsDir))
                    {
                        Directory.CreateDirectory(uploadsDir);
                    }

                    foreach (var file in ProjectImages)
                    {
                        if (file != null && file.Length > 0)
                        {
                            // Validate file type
                            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                            if (!allowedExtensions.Contains(fileExtension))
                            {
                                ModelState.AddModelError("ProjectImages", $"File {file.FileName} is not a valid image type. Only JPG, PNG, GIF, and SVG files are allowed.");
                                return View(project);
                            }

                            // Validate file size (max 10MB)
                            if (file.Length > 10 * 1024 * 1024)
                            {
                                ModelState.AddModelError("ProjectImages", $"File {file.FileName} is too large. File size must be less than 10MB.");
                                return View(project);
                            }

                            var fileName = GenerateUniqueFileName(file.FileName, uploadsDir);
                            var filePath = Path.Combine(uploadsDir, fileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }

                            allImagePaths.Add($"/uploads/projectpost/{fileName}");
                        }
                    }
                }

                // Update project images
                project.ImagePaths = allImagePaths.Any() ? System.Text.Json.JsonSerializer.Serialize(allImagePaths) : null;

                // Update project skills
                if (SelectedSkillIds != null && SelectedSkillIds.Any())
                {
                    // Remove existing project skills
                    var existingSkills = project.ProjectSkills.ToList();
                    dbContext.ProjectSkills.RemoveRange(existingSkills);

                    // Add new project skills
                    var newProjectSkills = SelectedSkillIds.Select(skillId => new ProjectSkill
                    {
                        ProjectId = project.Id,
                        UserSkillId = skillId
                    }).ToList();

                    await dbContext.ProjectSkills.AddRangeAsync(newProjectSkills);
                }

                await dbContext.SaveChangesAsync();

                // Redirect to prevent form resubmission on refresh
                return RedirectToAction("EditPost", new { Id = project.Id, message = "Project edited successfully!" });
            }
            else if (action == "delete")
            {
                dbContext.Projects.Remove(project);
                await dbContext.SaveChangesAsync();

                return RedirectToAction("Dashboard", "Client", new { message = "Project deleted successfully!" });
            }

            // If we reach here, there was an error, so return the view with the model
            return View(viewModel);
        }
        // Displays the bids for a specific project and allows the client to manage them.
        [HttpGet]
        public async Task<IActionResult> ManageBid(Guid id)
        {
            var project = await dbContext.Projects
                .Include(p => p.Biddings)
                .ThenInclude(b => b.User)
                .ThenInclude(u => u.UserAccountSkills)
                .ThenInclude(uas => uas.UserSkill)
                .Include(p => p.ProjectSkills)
                .ThenInclude(ps => ps.UserSkill)
                .Include(p => p.AcceptedBid)
                .ThenInclude(ab => ab.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
                return NotFound();

            // Create the view model
            var viewModel = new SmartHiringViewModel
            {
                Project = project
            };

            // Only generate recommendations if there are biddings and no accepted bid yet
            if (project.Biddings?.Any() == true && project.AcceptedBid == null)
            {
                try
                {
                    // Get AI recommendations
                    viewModel.Recommendations = await _smartHiringService.GetBestFreelancersAsync(id);

                    // Get project insights
                    viewModel.ProjectInsights = await _smartHiringService.GetProjectInsightsAsync(id);
                }
                catch (Exception ex)
                {
                    // Log the error but don't fail the page load
                    // You can add proper logging here
                    System.Diagnostics.Debug.WriteLine($"Error getting AI recommendations: {ex.Message}");

                    // Initialize empty recommendations and insights
                    viewModel.Recommendations = new List<SmartHiringPrediction>();
                    viewModel.ProjectInsights = new SmartHiringInsights
                    {
                        ProjectId = id,
                        TotalBidders = project.Biddings?.Count ?? 0,
                        AverageBidAmount = project.Biddings?.Any() == true ? project.Biddings.Average(b => b.Budget) : 0,
                        CompetitionLevel = "Unknown",
                        EstimatedHiringTime = "Unknown"
                    };
                }
            }
            else
            {
                // Initialize empty recommendations and basic insights
                viewModel.Recommendations = new List<SmartHiringPrediction>();
                viewModel.ProjectInsights = new SmartHiringInsights
                {
                    ProjectId = id,
                    TotalBidders = project.Biddings?.Count ?? 0,
                    AverageBidAmount = project.Biddings?.Any() == true ? project.Biddings.Average(b => b.Budget) : 0,
                    CompetitionLevel = project.Biddings?.Count > 10 ? "High" : project.Biddings?.Count > 5 ? "Medium" : "Low",
                    EstimatedHiringTime = project.Biddings?.Count > 5 ? "1-2 days" : "3-5 days"
                };
            }

            // Add mentorship completion data for each bidder
            if (project.Biddings != null)
            {
                foreach (var bid in project.Biddings)
                {
                    if (bid.User != null)
                    {
                        // Check if user has completed mentorship as mentor
                        var completedAsMentor = await dbContext.MentorshipMatches
                            .AnyAsync(mm => mm.MentorId == bid.User.Id && mm.Status == "Completed");

                        // Check if user has completed mentorship as mentee
                        var completedAsMentee = await dbContext.MentorshipMatches
                            .AnyAsync(mm => mm.MenteeId == bid.User.Id && mm.Status == "Completed");

                        // Store this data in ViewBag for the view to access
                        if (ViewBag.MentorshipData == null)
                            ViewBag.MentorshipData = new Dictionary<string, (bool, bool)>();

                        ViewBag.MentorshipData[bid.User.Id] = (completedAsMentor, completedAsMentee);
                    }
                }
            }

            return View(viewModel);
        }
        // Accepts a bid for a specific project, marking it as the accepted bid and updating the project accordingly.
        // Accepts a bid for a specific project, marking it as the accepted bid and updating the project accordingly.
        [HttpPost]
        public async Task<IActionResult> AcceptBid(Guid projectId, Guid bidId)
        {
            // Handle both cases: when projectId is passed separately and when only bidId is passed
            if (projectId == Guid.Empty)
            {
                // Find the project from the bidId
                var bidForProject = await dbContext.Biddings
                    .Include(b => b.Project)
                    .FirstOrDefaultAsync(b => b.Id == bidId);

                if (bidForProject != null)
                {
                    projectId = bidForProject.ProjectId;
                }
            }

            var project = await dbContext.Projects
                .Include(p => p.Biddings)
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
                return NotFound();

            var bidToAccept = project.Biddings.FirstOrDefault(b => b.Id == bidId);
            if (bidToAccept == null)
                return BadRequest("Invalid bid ID");

            // Delete any terminated contracts for this project before accepting the new bid
            var terminatedContracts = await dbContext.Contracts
                .Where(c => c.ProjectId == projectId && c.Status == "Terminated")
                .ToListAsync();

            foreach (var terminatedContract in terminatedContracts)
            {
                // First, find and remove contract terminations for this contract
                var contractTerminations = await dbContext.ContractTerminations
                    .Where(ct => ct.ContractId == terminatedContract.Id)
                    .ToListAsync();

                foreach (var termination in contractTerminations)
                {
                    // Remove contract termination audit logs
                    var terminationAuditLogs = await dbContext.ContractTerminationAuditLogs
                        .Where(ctal => ctal.ContractTerminationId == termination.Id)
                        .ToListAsync();

                    if (terminationAuditLogs.Any())
                    {
                        dbContext.ContractTerminationAuditLogs.RemoveRange(terminationAuditLogs);
                    }
                }

                // Remove contract terminations
                if (contractTerminations.Any())
                {
                    dbContext.ContractTerminations.RemoveRange(contractTerminations);
                }

                // Remove contract audit logs
                var contractAuditLogs = await dbContext.ContractAuditLogs
                    .Where(cal => cal.ContractId == terminatedContract.Id)
                    .ToListAsync();

                if (contractAuditLogs.Any())
                {
                    dbContext.ContractAuditLogs.RemoveRange(contractAuditLogs);
                }

                // Remove contract revisions
                var contractRevisions = await dbContext.ContractRevisions
                    .Where(cr => cr.ContractId == terminatedContract.Id)
                    .ToListAsync();

                if (contractRevisions.Any())
                {
                    dbContext.ContractRevisions.RemoveRange(contractRevisions);
                }

                // Remove any deliverables associated with the terminated contract
                var deliverables = await dbContext.Deliverables
                    .Where(d => d.ContractId == terminatedContract.Id)
                    .ToListAsync();

                if (deliverables.Any())
                {
                    dbContext.Deliverables.RemoveRange(deliverables);
                }

                // Finally, remove the terminated contract
                dbContext.Contracts.Remove(terminatedContract);
            }

            foreach (var bid in project.Biddings)
            {
                bid.IsAccepted = (bid.Id == bidId);
                if (bid.Id == bidId)
                {
                    bid.BiddingAcceptedDate = DateTime.UtcNow.ToLocalTime();
                }
            }

            project.AcceptedBidId = bidId;
            project.Status = "Active"; // Set project status to Active when bid is accepted

            await dbContext.SaveChangesAsync();

            // Get the freelancer's information for the notification
            var freelancer = await dbContext.UserAccounts.FindAsync(bidToAccept.UserId);

            // Create notification for the freelancer whose bid was accepted
            var notificationTitle = "Bid Accepted!";
            var notificationMessage = $"Congratulations! Your bid on '{project.ProjectName}' has been accepted by {project.User?.FirstName} {project.User?.LastName} for ₱{bidToAccept.Budget:N0}";
            var notificationIconSvg = "<svg fill=\"currentColor\" version=\"1.1\" id=\"Capa_1\" xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" viewBox=\"0 0 47.001 47.001\" xml:space=\"preserve\"><g id=\"SVGRepo_bgCarrier\" stroke-width=\"0\"></g><g id=\"SVGRepo_tracerCarrier\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></g><g id=\"SVGRepo_iconCarrier\"> <g> <g id=\"Layer_1_120_\"> <g> <g> <path d=\"M31.736,10.307c-0.111-0.112-0.249-0.193-0.398-0.24l-8.975-2.818c-3.589-1.127-5.924,0.839-6.553,1.47 c-0.367,0.367-0.648,0.754-0.792,1.091l-3.998,9.404c-0.229,0.538-0.151,1.255,0.208,1.97c0.514,1.021,1.44,1.757,2.547,2.022 c1.239,0.297,2.524-0.106,3.53-1.111c0.263-0.263,0.487-0.553,0.619-0.799l1.344-2.493c0.221-0.413,0.542-0.841,0.88-1.179 c1.153-1.154,1.701-0.626,1.934-0.402c2.011,1.941,12.554,12.529,12.554,12.529c0.375,0.375,0.297,1.086-0.172,1.554 c-0.468,0.467-1.18,0.547-1.554,0.174l-2.962-2.961c-0.382-0.383-0.998-0.383-1.38,0c-0.382,0.379-0.382,0.998,0,1.379 l2.962,2.963c0.374,0.373,0.296,1.084-0.172,1.551c-0.468,0.469-1.181,0.547-1.553,0.174l-2.963-2.961 c-0.382-0.382-1-0.382-1.38,0c-0.382,0.379-0.382,0.998,0,1.38l2.962,2.962c0.374,0.374,0.293,1.085-0.174,1.553 c-0.467,0.467-1.178,0.547-1.553,0.172l-2.962-2.961c-0.38-0.381-0.999-0.381-1.38,0c-0.38,0.381-0.38,1,0,1.379l2.962,2.963 c0.375,0.375,0.295,1.086-0.172,1.554c-0.47,0.468-1.181,0.547-1.554,0.173l-3.606-3.609c0.515-0.774,0.375-1.897-0.389-2.664 c-0.856-0.855-2.173-0.934-2.935-0.17c0.762-0.763,0.687-2.078-0.171-2.935c-0.858-0.856-2.172-0.935-2.934-0.173 c0.762-0.762,0.685-2.076-0.174-2.932c-0.856-0.858-2.17-0.936-2.934-0.174c0.764-0.762,0.685-2.076-0.172-2.935 c-0.802-0.802-1.997-0.911-2.774-0.3l-5.839-5.839c-0.381-0.382-1-0.382-1.381,0c-0.38,0.38-0.38,0.999,0,1.381l5.824,5.823 l-1.727,1.727c-0.762,0.761-0.685,2.075,0.174,2.934c0.856,0.856,2.17,0.935,2.933,0.172c-0.763,0.763-0.685,2.076,0.173,2.934 c0.856,0.855,2.171,0.936,2.934,0.173c-0.763,0.763-0.686,2.076,0.172,2.933c0.858,0.858,2.172,0.936,2.934,0.174 c-0.762,0.761-0.685,2.074,0.173,2.933c0.857,0.856,2.17,0.935,2.934,0.172l1.824-1.823l3.581,3.58 c1.143,1.143,3.076,1.063,4.314-0.173c0.603-0.603,0.925-1.373,0.97-2.135c0.762-0.045,1.533-0.368,2.135-0.972 c0.604-0.603,0.928-1.373,0.974-2.135c0.761-0.045,1.529-0.367,2.135-0.971c0.603-0.604,0.926-1.373,0.97-2.136 c0.763-0.044,1.533-0.366,2.137-0.972c1.236-1.236,1.312-3.172,0.172-4.313l-1.51-1.511l6.2-6.199 c0.381-0.38,0.381-0.999,0-1.38L31.736,10.307z\"></path> </g> <g> <path d=\"M46.43,12.489l-7.901-7.901c-0.762-0.763-1.999-0.763-2.762,0l-2.762,2.76c-0.762,0.762-0.762,1.999,0,2.761 l7.902,7.903c0.763,0.762,2,0.762,2.762,0l2.761-2.761C47.191,14.488,47.191,13.251,46.43,12.489z M42.283,16.513 c-0.884,0-1.602-0.717-1.602-1.602c0-0.885,0.718-1.603,1.602-1.603c0.885,0,1.603,0.717,1.603,1.603 C43.885,15.795,43.168,16.513,42.283,16.513z\"></path> </g> </g> </g> </g> </g></svg>";
            var relatedUrl = $"/ManageProjectFreelancer/Details/{projectId}";

            await notificationService.CreateNotificationAsync(
                bidToAccept.UserId,
                notificationTitle,
                notificationMessage,
                "bid_accepted",
                notificationIconSvg,
                relatedUrl
            );

            return RedirectToAction("ManageBid", new { id = projectId });
        }

        [HttpGet]
        public async Task<IActionResult> Profile(string? id = null)
        {
            // Get the current user's ID
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized();

            // If no id is provided, show current user's profile
            var targetUserId = string.IsNullOrEmpty(id) ? currentUserId : id;

            // Get client profile with all related data
            var client = await dbContext.UserAccounts
                .Include(u => u.Projects)
                .ThenInclude(p => p.Biddings)
                .FirstOrDefaultAsync(u => u.Id == targetUserId);

            if (client == null)
                return NotFound();

            // Get client's projects
            var projects = await dbContext.Projects
                .Where(p => p.UserId == targetUserId)
                .OrderByDescending(p => p.CreatedAt)
                .Take(10)
                .ToListAsync();

            // Check identity verification status
            var identityVerification = await dbContext.IdentityVerifications
                .FirstOrDefaultAsync(iv => iv.UserAccountId == targetUserId);
            var isVerified = identityVerification?.Status == "APPROVED";
            var isRejected = identityVerification?.Status == "REJECTED";
            var isPending = identityVerification?.Status == "PENDING";

            var isOwnProfile = (currentUserId == targetUserId);

            ViewBag.Projects = projects;
            ViewBag.IsOwnProfile = isOwnProfile;
            ViewBag.IsVerified = isVerified;
            ViewBag.IsPending = isPending;
            ViewBag.IsRejected = isRejected;
            ViewBag.ShowContactInfo = isOwnProfile || isVerified;

            return View(client);
        }

        [HttpGet]
        public async Task<IActionResult> EditAccount()
        {
            // Get the user ID from the claims
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Fetch the user account details from the database
            var userAccount = await dbContext.UserAccounts.FindAsync(userId);
            if (userAccount == null)
                return NotFound();

            var identityVerification = await dbContext.IdentityVerifications
                .FirstOrDefaultAsync(iv => iv.UserAccountId == userId);
            var isVerified = identityVerification?.Status == "APPROVED";

            ViewBag.IsVerified = isVerified;

            var twoFactorEnabled = await _userManager.GetTwoFactorEnabledAsync(userAccount);
            ViewBag.TwoFactorEnabled = twoFactorEnabled;

            // Create view model
            var viewModel = new EditAccount
            {
                FirstName = userAccount.FirstName,
                LastName = userAccount.LastName,
                Email = userAccount.Email,
                UserName = userAccount.UserName,
                Bio = userAccount.Bio,
                Photo = userAccount.Photo
            };

            if (TempData.ContainsKey("Message"))
                ViewBag.Message = TempData["Message"];
            if (TempData.ContainsKey("ErrorMessage"))
                ViewBag.ErrorMessage = TempData["ErrorMessage"];
            if (TempData.ContainsKey("MessageType"))
                ViewBag.MessageType = TempData["MessageType"];

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> EditAccount(EditAccount viewModel, IFormFile? PhotoFile)
        {
            if (!ModelState.IsValid)
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var reloadedUser = userId != null ? await _userManager.FindByIdAsync(userId) : null;
                ViewBag.TwoFactorEnabled = reloadedUser != null && await _userManager.GetTwoFactorEnabledAsync(reloadedUser);

                return View(viewModel);
            }

            // Get user ID
            var userId2 = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId2))
                return Unauthorized();

            // Fetch user account
            var userAccount = await dbContext.UserAccounts.FindAsync(userId2);
            if (userAccount == null)
                return NotFound();

            ViewBag.TwoFactorEnabled = await _userManager.GetTwoFactorEnabledAsync(userAccount);

            // Check for existing username/email
            var existingUserWithUsername = await dbContext.UserAccounts
                .FirstOrDefaultAsync(u => u.UserName == viewModel.UserName && u.Id.ToString() != userId2);
            var existingUserWithEmail = await dbContext.UserAccounts
                .FirstOrDefaultAsync(u => u.Email == viewModel.Email && u.Id.ToString() != userId2);

            if (existingUserWithEmail != null)
            {
                ModelState.AddModelError("Email", "Email is already registered.");
                return View(viewModel);
            }

            if (existingUserWithUsername != null)
            {
                ModelState.AddModelError("UserName", "Username is already taken.");
                return View(viewModel);
            }

            // Track if any changes were made
            bool hasChanges = false;
            bool nameChanged = false;
            bool photoChanged = false;

            // Check and update user account fields only if they changed
            if (userAccount.FirstName != viewModel.FirstName)
            {
                userAccount.FirstName = viewModel.FirstName;
                hasChanges = true;
                nameChanged = true;
            }

            if (userAccount.LastName != viewModel.LastName)
            {
                userAccount.LastName = viewModel.LastName;
                hasChanges = true;
                nameChanged = true;
            }

            if (userAccount.Email != viewModel.Email)
            {
                userAccount.Email = viewModel.Email;
                hasChanges = true;
            }

            if (userAccount.UserName != viewModel.UserName)
            {
                userAccount.UserName = viewModel.UserName;
                hasChanges = true;
            }

            if (userAccount.Bio != viewModel.Bio)
            {
                userAccount.Bio = viewModel.Bio;
                hasChanges = true;
            }

            // Handle photo upload
            if (PhotoFile != null && PhotoFile.Length > 0)
            {
                // Validate the uploaded file type and size
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var fileExtension = Path.GetExtension(PhotoFile.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    ModelState.AddModelError("PhotoFile", "Please upload a valid image file (jpg, jpeg, png, gif).");
                    return View(viewModel);
                }

                if (PhotoFile.Length > 10 * 1024 * 1024) // 10 MB limit
                {
                    ModelState.AddModelError("PhotoFile", "The image file size should not exceed 10 MB.");
                    return View(viewModel);
                }

                // Generate a unique file name while preserving original name
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "profiles");

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var fileName = GenerateUniqueFileName(PhotoFile.FileName, uploadsFolder);
                var filePath = Path.Combine(uploadsFolder, fileName);

                // Delete old photo if exists
                if (!string.IsNullOrEmpty(userAccount.Photo))
                {
                    var oldPhotoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", userAccount.Photo.TrimStart('/'));
                    if (System.IO.File.Exists(oldPhotoPath))
                    {
                        try
                        {
                            System.IO.File.Delete(oldPhotoPath);
                        }
                        catch
                        {
                        }
                    }
                }

                // Save the uploaded photo
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await PhotoFile.CopyToAsync(stream);
                }

                userAccount.Photo = $"/uploads/profiles/{fileName}";
                viewModel.Photo = userAccount.Photo;
                hasChanges = true;
                photoChanged = true;
            }

            // Only save if there were actual changes
            if (hasChanges)
            {
                // Update normalized fields through UserManager to ensure Identity consistency
                if (userAccount.Email != viewModel.Email)
                {
                    var emailResult = await _userManager.SetEmailAsync(userAccount, viewModel.Email);
                    if (!emailResult.Succeeded)
                    {
                        foreach (var error in emailResult.Errors)
                        {
                            ModelState.AddModelError("Email", error.Description);
                        }
                        return View(viewModel);
                    }
                    
                    // Explicitly update the normalized email to ensure it's updated
                    userAccount.NormalizedEmail = viewModel.Email.ToUpperInvariant();
                }

                if (userAccount.UserName != viewModel.UserName)
                {
                    var usernameResult = await _userManager.SetUserNameAsync(userAccount, viewModel.UserName);
                    if (!usernameResult.Succeeded)
                    {
                        foreach (var error in usernameResult.Errors)
                        {
                            ModelState.AddModelError("UserName", error.Description);
                        }
                        return View(viewModel);
                    }
                    
                    // Explicitly update the normalized username to ensure it's updated
                    userAccount.NormalizedUserName = viewModel.UserName.ToUpperInvariant();
                }

                // Save other changes to the database
                dbContext.Entry(userAccount).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                await dbContext.SaveChangesAsync();

                // Force a complete refresh from the database to get the updated normalized fields
                await dbContext.Entry(userAccount).ReloadAsync();
                
                // Also update the local object properties to ensure consistency
                userAccount.Email = viewModel.Email;
                userAccount.UserName = viewModel.UserName;

                // Always refresh claims when there are changes since all fields affect claims
                await RefreshUserClaims(userAccount);

                ViewBag.Message = "Account updated successfully!";
            }
            else
            {
                ViewBag.Message = "No changes were detected.";
            }

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> RequestEmailChange([FromBody] EmailChangeRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.NewEmail))
            {
                TempData["ErrorMessage"] = "Invalid request data provided.";
                TempData["MessageType"] = "error";
                return Json(new { success = false, reload = true });
            }

            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    TempData["ErrorMessage"] = "User not authenticated. Please log in again.";
                    TempData["MessageType"] = "error";
                    return Json(new { success = false, reload = true });
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "User account not found.";
                    TempData["MessageType"] = "error";
                    return Json(new { success = false, reload = true });
                }

                var existingUser = await _userManager.FindByEmailAsync(request.NewEmail);
                if (existingUser != null)
                {
                    TempData["ErrorMessage"] = "This email address is already registered with another account.";
                    TempData["MessageType"] = "error";
                    return Json(new { success = false, reload = true });
                }

                if (!IsValidEmail(request.NewEmail))
                {
                    TempData["ErrorMessage"] = "Please enter a valid email address.";
                    TempData["MessageType"] = "error";
                    return Json(new { success = false, reload = true });
                }

                var token = await _userManager.GenerateChangeEmailTokenAsync(user, request.NewEmail);
                var confirmationLink = Url.Action("ConfirmEmailChange", "Client",
                    new { userId = userId, email = request.NewEmail, token = token },
                    Request.Scheme);

                await _emailService.SendEmailChangeConfirmationAsync(request.NewEmail, confirmationLink);

                TempData["Message"] = $"Confirmation email sent successfully to <span class=\"font-bold\">{request.NewEmail}</span>! Please confirm to complete the change.";
                TempData["MessageType"] = "success";
                return Json(new { success = true, reload = true });
            }
            catch
            {
                TempData["ErrorMessage"] = "An unexpected error occurred while processing your request. Please try again later.";
                TempData["MessageType"] = "error";
                return Json(new { success = false, reload = true });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ConfirmEmailChange(string userId, string email, string token)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            var result = await _userManager.ChangeEmailAsync(user, email, token);
            if (result.Succeeded)
            {
                // Update normalized email
                user.NormalizedEmail = email.ToUpperInvariant();
                await _userManager.UpdateNormalizedEmailAsync(user);
                await RefreshUserClaims(user);

                TempData["Message"] = "Your email has been successfully changed.";
                TempData["MessageType"] = "success";
                return RedirectToAction("EditAccount");
            }

            TempData["ErrorMessage"] = "Error changing email. Please try again.";
            TempData["MessageType"] = "error";
            return RedirectToAction("EditAccount");
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
        private async Task RefreshUserClaims(UserAccount userAccount)
        {
            await _signInManager.RefreshSignInAsync(userAccount);
        }
    }
}
