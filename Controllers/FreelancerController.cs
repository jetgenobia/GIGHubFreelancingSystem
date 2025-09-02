using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Freelancing.Data;
using Freelancing.Models;
using Freelancing.Models.Entities;
using Freelancing.Services;
using System;
using Microsoft.CodeAnalysis;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;

namespace Freelancing.Controllers
{
    // Handles freelancer-specific functionalities such as viewing projects, bidding on projects, and managing bids.
    [Authorize(Roles = "Freelancer")]
    public class FreelancerController : Controller
    {
        private readonly ApplicationDbContext dbContext;
        private readonly INotificationService notificationService;
        private readonly UserManager<UserAccount> _userManager;
        private readonly SignInManager<UserAccount> _signInManager;
        private readonly IEmailService _emailService;

        public FreelancerController(ApplicationDbContext context, INotificationService notificationService, UserManager<UserAccount> userManager, SignInManager<UserAccount> signInManager, IEmailService emailService)
        {
            this.dbContext = context;
            this.notificationService = notificationService;
            this._userManager = userManager;
            this._signInManager = signInManager;
            this._emailService = emailService;
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
        public IActionResult Index()
        {
            return View();
        }
        // Displays the freelancer dashboard with statistics and a list of biddings for the logged-in user.
        public async Task<IActionResult> Dashboard(Guid projectId)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            int totalAccepted = dbContext.Biddings.Count(p => p.UserId == userId && p.IsAccepted != false);
            ViewBag.TotalAccepted = totalAccepted;

            var biddings = await dbContext.Biddings
                .Include(b => b.Project)
                .ThenInclude(p => p.User)
                .Where(b => b.UserId == userId)
                .ToListAsync();

            var projects = await dbContext.Projects
                .Include(p => p.Biddings)
                .ThenInclude(b => b.User)
                .FirstOrDefaultAsync(p => p.Id == projectId);


            var viewModel = new FreelancerDashboard
            {
                Biddings = biddings,
                Project = projects
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
        // Displays the details of a specific project, including its bids and the user who posted it.
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
        // Allows a freelancer to place a bid on a project. If the freelancer has already placed a bid, it redirects them with a message.
        [HttpGet]
        public async Task<IActionResult> Bid(Guid id)
        {
            var project = await dbContext.Projects.FindAsync(id);
            if (project == null)
            {
                return NotFound();
            }

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            bool alreadyBid = await dbContext.Biddings.AnyAsync(b => b.UserId == userId && b.ProjectId == project.Id);
            if (alreadyBid)
            {
                TempData["Message"] = "You have already placed a bid on this project.";
                return RedirectToAction("Project", new { id = project.Id });
            }

            var viewModel = new ViewProjectandBidding
            {
                Project = project,
                Bidding = new AddBidding()
            };
            return View(viewModel);
        }
        // Handles the submission of a bid on a project.
        [HttpPost]
        public async Task<IActionResult> Bid(ViewProjectandBidding viewModel)
        {
            var project = await dbContext.Projects.FindAsync(viewModel.Project.Id);
            if (project == null)
                return NotFound();

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            bool alreadyBid = await dbContext.Biddings.AnyAsync(b => b.UserId == userId && b.ProjectId == project.Id);
            if (alreadyBid)
            {
                viewModel.Project = project;
                return View(viewModel);
            }

            // Handle file uploads for previous works
            var uploadedFilePaths = new List<string>();

            // Process newly uploaded files
            if (viewModel.Bidding.PreviousWorksFiles != null && viewModel.Bidding.PreviousWorksFiles.Any())
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".svg", ".pdf", ".doc", ".docx", ".txt", ".zip", ".mp4", ".mov", ".avi" };
                const int maxFileSize = 10 * 1024 * 1024; // 10MB

                foreach (var file in viewModel.Bidding.PreviousWorksFiles)
                {
                    if (file.Length > 0)
                    {
                        // Validate file type
                        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                        if (!allowedExtensions.Contains(fileExtension))
                        {
                            ModelState.AddModelError("PreviousWorksFiles", $"File {file.FileName} is not a valid file type. Allowed types: {string.Join(", ", allowedExtensions)}");
                            viewModel.Project = project;
                            return View(viewModel);
                        }

                        // Validate file size
                        if (file.Length > maxFileSize)
                        {
                            ModelState.AddModelError("PreviousWorksFiles", $"File {file.FileName} is too large. File size must be less than 10MB.");
                            viewModel.Project = project;
                            return View(viewModel);
                        }

                        // Generate unique filename while preserving original name
                        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "previous-works");

                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                        }

                        var fileName = GenerateUniqueFileName(file.FileName, uploadsFolder);
                        var filePath = Path.Combine(uploadsFolder, fileName);
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        uploadedFilePaths.Add($"/uploads/previous-works/{fileName}");
                    }
                }
            }



            var bidding = new Bidding
            {
                UserId = userId,
                ProjectId = project.Id,
                Budget = viewModel.Bidding.Budget,
                Delivery = viewModel.Bidding.Delivery,
                Proposal = viewModel.Bidding.Proposal,
                PreviousWorksPaths = uploadedFilePaths.Any() ? JsonSerializer.Serialize(uploadedFilePaths) : null,
                RepositoryLinks = !string.IsNullOrEmpty(viewModel.Bidding.RepositoryLinks) ? viewModel.Bidding.RepositoryLinks : null
            };

            dbContext.Biddings.Add(bidding);
            await dbContext.SaveChangesAsync();

            // Get the freelancer's information for the notification
            var freelancer = await dbContext.UserAccounts.FindAsync(userId);

            // Create notification for the project owner
            var notificationTitle = "New Bid Received";
            var notificationMessage = $"You received a new bid from {freelancer?.FirstName} {freelancer?.LastName} on your project '{project.ProjectName}'";
            var notificationIconSvg = "<svg viewBox=\"0 0 24 24\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\"><g id=\"SVGRepo_bgCarrier\" stroke-width=\"0\"></g><g id=\"SVGRepo_tracerCarrier\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></g><g id=\"SVGRepo_iconCarrier\"> <path fill-rule=\"evenodd\" clip-rule=\"evenodd\" d=\"M17.4964 21.9284C17.844 21.7894 18.1491 21.6495 18.4116 21.5176C18.9328 22.4046 19.8969 23 21 23C22.6569 23 24 21.6568 24 20V14C24 12.3431 22.6569 11 21 11C19.5981 11 18.4208 11.9616 18.0917 13.2612C17.8059 13.3614 17.5176 13.4549 17.2253 13.5384C16.3793 13.7801 15.3603 13.9999 14.5 13.9999C13.2254 13.9999 10.942 13.5353 9.62034 13.2364C8.61831 13.0098 7.58908 13.5704 7.25848 14.5622L6.86313 15.7483C5.75472 15.335 4.41275 14.6642 3.47619 14.1674C2.42859 13.6117 1.09699 14.0649 0.644722 15.1956L0.329309 15.9841C0.0210913 16.7546 0.215635 17.6654 0.890813 18.2217C1.66307 18.8581 3.1914 20.0378 5.06434 21.063C6.91913 22.0782 9.21562 22.9999 11.5 22.9999C14.1367 22.9999 16.1374 22.472 17.4964 21.9284ZM20 20C20 20.5523 20.4477 21 21 21C21.5523 21 22 20.5523 22 20V14C22 13.4477 21.5523 13 21 13C20.4477 13 20 13.4477 20 14V20ZM14.5 15.9999C12.9615 15.9999 10.4534 15.4753 9.17918 15.1872C9.17918 15.1872 8.84483 16.1278 8.7959 16.2745L12.6465 17.2776C13.1084 17.3979 13.372 17.8839 13.2211 18.3367C13.0935 18.7194 12.7092 18.9536 12.3114 18.8865C11.0903 18.6805 8.55235 18.2299 7.25848 17.8365C5.51594 17.3066 3.71083 16.5559 2.53894 15.9342C2.53894 15.9342 2.22946 16.6189 2.19506 16.7049C2.92373 17.3031 4.32792 18.3799 6.0246 19.3086C7.76488 20.2611 9.70942 20.9999 11.5 20.9999C15.023 20.9999 17.1768 19.9555 18 19.465V15.3956C16.8681 15.7339 15.6865 15.9999 14.5 15.9999Z\" fill=\"#0F0F0F\"></path> <path d=\"M12 1C11.4477 1 11 1.44772 11 2V7.58564L9.7071 6.29278C9.3166 5.9024 8.68342 5.9024 8.29292 6.29278C7.90235 6.68341 7.90235 7.31646 8.29292 7.70709L11.292 10.7063C11.6823 11.0965 12.3149 11.0968 12.7055 10.707L15.705 7.71368C16.0955 7.3233 16.0955 6.69 15.705 6.29962C15.3145 5.90899 14.6813 5.90899 14.2908 6.29962L13 7.59034V2C13 1.44772 12.5523 1 12 1Z\" fill=\"#0F0F0F\"></path> </g></svg>";
            var relatedUrl = $"/Client/ManageBid/{project.Id}";

            await notificationService.CreateNotificationAsync(
                project.UserId,
                notificationTitle,
                notificationMessage,
                "bid",
                notificationIconSvg,
                relatedUrl
            );

            ModelState.Clear();
            ViewBag.Message = "Bidded successfully!";

            return RedirectToAction("Project", new { id = project.Id });
        }
        // Allows a freelancer to edit an existing bid on a project.
        [HttpGet]
        public async Task<IActionResult> EditBid(Guid id)
        {
            var bidding = await dbContext.Biddings
                .Include(b => b.Project)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bidding == null)
                return NotFound();

            var viewModel = new ViewProjectandBidding
            {
                Project = bidding.Project,
                Bidding = new AddBidding
                {
                    UserId = bidding.UserId,
                    ProjectId = bidding.ProjectId,
                    Budget = bidding.Budget,
                    Delivery = bidding.Delivery,
                    Proposal = bidding.Proposal,
                    PreviousWorksPaths = bidding.PreviousWorksPaths,
                    RepositoryLinks = bidding.RepositoryLinks
                }
            };
            return View(viewModel);
        }
        // Handles the submission of an edited bid on a project. It allows saving changes or deleting the bid.
        [HttpPost]
        public async Task<IActionResult> EditBid(Guid id, ViewProjectandBidding viewModel, string action, string removedFiles)
        {
            var bidding = await dbContext.Biddings.FindAsync(id);
            if (bidding == null)
                return NotFound();

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || bidding.UserId != userId)
                return Unauthorized();

            if (action == "save")
            {
                bidding.Budget = viewModel.Bidding.Budget;
                bidding.Delivery = viewModel.Bidding.Delivery;
                bidding.Proposal = viewModel.Bidding.Proposal;
                bidding.RepositoryLinks = !string.IsNullOrEmpty(viewModel.Bidding.RepositoryLinks) ? viewModel.Bidding.RepositoryLinks : null;

                // Handle file uploads for previous works
                var existingFilePaths = new List<string>();
                if (!string.IsNullOrEmpty(bidding.PreviousWorksPaths))
                {
                    try
                    {
                        existingFilePaths = JsonSerializer.Deserialize<List<string>>(bidding.PreviousWorksPaths) ?? new List<string>();
                    }
                    catch
                    {
                        existingFilePaths = new List<string>();
                    }
                }

                // Handle file removal
                var filesToRemove = new List<string>();
                if (!string.IsNullOrEmpty(removedFiles))
                {
                    try
                    {
                        filesToRemove = JsonSerializer.Deserialize<List<string>>(removedFiles) ?? new List<string>();
                    }
                    catch
                    {
                        filesToRemove = new List<string>();
                    }
                }

                // Remove files from existing paths and delete from disk
                foreach (var filePath in filesToRemove)
                {
                    if (existingFilePaths.Contains(filePath))
                    {
                        existingFilePaths.Remove(filePath);

                        // Delete the physical file
                        var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", filePath.TrimStart('/'));
                        if (System.IO.File.Exists(fullPath))
                        {
                            try
                            {
                                System.IO.File.Delete(fullPath);
                            }
                            catch (Exception ex)
                            {
                                // Log the error but don't fail the operation
                                Console.WriteLine($"Error deleting file {fullPath}: {ex.Message}");
                            }
                        }
                    }
                }

                var uploadedFilePaths = new List<string>();

                if (viewModel.Bidding.PreviousWorksFiles != null && viewModel.Bidding.PreviousWorksFiles.Any())
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".svg", ".pdf", ".doc", ".docx", ".txt", ".zip", ".mp4", ".mov", ".avi" };
                    const int maxFileSize = 10 * 1024 * 1024; // 10MB

                    foreach (var file in viewModel.Bidding.PreviousWorksFiles)
                    {
                        if (file.Length > 0)
                        {
                            // Validate file type
                            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                            if (!allowedExtensions.Contains(fileExtension))
                            {
                                ModelState.AddModelError("PreviousWorksFiles", $"File {file.FileName} is not a valid file type. Allowed types: {string.Join(", ", allowedExtensions)}");
                                viewModel.Project = bidding.Project;
                                return View(viewModel);
                            }

                            // Validate file size
                            if (file.Length > maxFileSize)
                            {
                                ModelState.AddModelError("PreviousWorksFiles", $"File {file.FileName} is too large. File size must be less than 10MB.");
                                viewModel.Project = bidding.Project;
                                return View(viewModel);
                            }

                            // Generate unique filename while preserving original name
                            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "previous-works");

                            if (!Directory.Exists(uploadsFolder))
                            {
                                Directory.CreateDirectory(uploadsFolder);
                            }

                            var fileName = GenerateUniqueFileName(file.FileName, uploadsFolder);
                            var filePath = Path.Combine(uploadsFolder, fileName);
                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }

                            uploadedFilePaths.Add($"/uploads/previous-works/{fileName}");
                        }
                    }
                }

                // Combine existing and new file paths
                var allFilePaths = existingFilePaths.Concat(uploadedFilePaths).ToList();
                bidding.PreviousWorksPaths = allFilePaths.Any() ? JsonSerializer.Serialize(allFilePaths) : null;

                await dbContext.SaveChangesAsync();
                ViewBag.Message = "Bid edited successfully!";

                var updatedBidding = await dbContext.Biddings
                    .Include(b => b.Project)
                    .FirstOrDefaultAsync(b => b.Id == id);

                viewModel.Project = updatedBidding.Project;
                viewModel.Bidding.PreviousWorksPaths = updatedBidding.PreviousWorksPaths;
                viewModel.Bidding.RepositoryLinks = updatedBidding.RepositoryLinks;
                return View(viewModel);
            }
            else if (action == "delete")
            {
                // Delete all associated files before removing the bidding
                if (!string.IsNullOrEmpty(bidding.PreviousWorksPaths))
                {
                    try
                    {
                        var filePaths = JsonSerializer.Deserialize<List<string>>(bidding.PreviousWorksPaths) ?? new List<string>();
                        foreach (var filePath in filePaths)
                        {
                            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", filePath.TrimStart('/'));
                            if (System.IO.File.Exists(fullPath))
                            {
                                try
                                {
                                    System.IO.File.Delete(fullPath);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Error deleting file {fullPath}: {ex.Message}");
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Continue with deletion even if file cleanup fails
                    }
                }

                dbContext.Biddings.Remove(bidding);
                await dbContext.SaveChangesAsync();
            }

            return RedirectToAction("Dashboard", "Freelancer");
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

            // Get freelancer profile with all related data
            var freelancer = await dbContext.UserAccounts
                .Include(u => u.UserAccountSkills)
                .ThenInclude(uas => uas.UserSkill)
                .Include(u => u.Portfolios)
                .FirstOrDefaultAsync(u => u.Id == targetUserId);

            // Debug: Check if portfolios are loaded
            var portfolioCount = freelancer?.Portfolios?.Count ?? 0;
            System.Diagnostics.Debug.WriteLine($"Portfolio count for user {targetUserId}: {portfolioCount}");

            if (freelancer == null)
                return NotFound();

            // Check mentorship completion status
            var completedAsMentor = await dbContext.MentorshipMatches
                .AnyAsync(mm => mm.MentorId == targetUserId && mm.Status == "Completed");

            var completedAsMentee = await dbContext.MentorshipMatches
                .AnyAsync(mm => mm.MenteeId == targetUserId && mm.Status == "Completed");

            // Get user's projects and biddings for portfolio
            var projects = await dbContext.Projects
                .Where(p => p.UserId == targetUserId)
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .ToListAsync();

            var biddings = await dbContext.Biddings
                .Include(b => b.Project)
                .Where(b => b.UserId == targetUserId && b.IsAccepted == true)
                .Take(5)
                .ToListAsync();

            // FIX: Use targetUserId instead of id for feedbacks
            var feedbacks = await dbContext.FreelancerFeedbacks
                .Where(f => f.FreelancerId == targetUserId) // Changed from id to targetUserId
                .Include(f => f.AcceptBidding)
                .ThenInclude(ab => ab.Project)
                .ThenInclude(p => p.User)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            var feedbackDtos = feedbacks.Select(f => new FeedbackDto
            {
                Id = f.Id,
                Rating = f.Rating,
                WouldRecommend = f.WouldRecommend,
                Comments = f.Comments,
                CreatedAt = f.CreatedAt,
                User = new UserDto
                {
                    Id = f.AcceptBidding.Project.User.Id,
                    FirstName = f.AcceptBidding.Project.User.FirstName,
                    LastName = f.AcceptBidding.Project.User.LastName,
                    Email = f.AcceptBidding.Project.User.Email,
                    UserName = f.AcceptBidding.Project.User.UserName,
                    Photo = f.AcceptBidding.Project.User.Photo
                }
            }).ToList();

            // Calculate average rating
            var averageRating = feedbacks.Any() ? feedbacks.Average(f => f.Rating) : 0;

            // Calculate recommendation rate
            var recommendationRate = feedbacks.Any() ?
                (double)feedbacks.Count(f => f.WouldRecommend) / feedbacks.Count * 100 : 0;

            ViewBag.MentorshipData = (completedAsMentor, completedAsMentee);
            ViewBag.Projects = projects;
            ViewBag.Biddings = biddings;
            ViewBag.IsOwnProfile = (currentUserId == targetUserId); // Flag to indicate if this is user's own profile
            ViewBag.AllFeedbacks = feedbacks;
            ViewBag.RecentFeedbacks = feedbacks;
            ViewBag.FeedbackDtos = feedbackDtos;
            ViewBag.AverageRating = averageRating;
            ViewBag.RecommendationRate = recommendationRate;
            ViewBag.FeedbackCount = feedbacks.Count;

            return View(freelancer);
        }

        [HttpGet]
        public async Task<IActionResult> EditAccount()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var userAccount = await dbContext.UserAccounts.FindAsync(userId);
            if (userAccount == null)
                return NotFound();

            var savedSkills = await dbContext.UserAccountSkills
                .Where(uas => uas.UserAccountId == userId)
                .Include(uas => uas.UserSkill)
                .Select(uas => uas.UserSkill)
                .OrderBy(s => s.Name)
                .ToListAsync();

            // Check for completed mentorship relationships
            var completedAsMentor = await dbContext.MentorshipMatches
                .AnyAsync(mm => mm.MentorId == userId && mm.Status == "Completed");

            var completedAsMentee = await dbContext.MentorshipMatches
                .AnyAsync(mm => mm.MenteeId == userId && mm.Status == "Completed");

            var viewModel = new EditAccount
            {
                UserId = Guid.Parse(userId),
                FirstName = userAccount.FirstName,
                LastName = userAccount.LastName,
                Email = userAccount.Email,
                UserName = userAccount.UserName,
                Photo = userAccount.Photo,
                Bio = userAccount.Bio,
                ExperienceLevel = userAccount.ExperienceLevel,
                SavedSkills = savedSkills,
                TotalSkillsCount = savedSkills.Count,
                HasCompletedMentorshipAsMentor = completedAsMentor,
                HasCompletedMentorshipAsMentee = completedAsMentee
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
                // Reload necessary data for the view
                var reloadedViewModel = await PopulateEditAccountViewModel(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, null);
                // Preserve form data
                reloadedViewModel.FirstName = viewModel.FirstName;
                reloadedViewModel.LastName = viewModel.LastName;
                reloadedViewModel.Email = viewModel.Email;
                reloadedViewModel.UserName = viewModel.UserName;
                reloadedViewModel.Bio = viewModel.Bio;
                reloadedViewModel.ExperienceLevel = viewModel.ExperienceLevel;

                return View(reloadedViewModel);
            }

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var userAccount = await _userManager.FindByIdAsync(userId);
            if (userAccount == null)
                return NotFound();

            // Check for existing username/email (excluding current user)
            var existingUserWithUsername = await _userManager.Users
                .FirstOrDefaultAsync(u => u.UserName == viewModel.UserName && u.Id != userId);
            var existingUserWithEmail = await _userManager.Users
                .FirstOrDefaultAsync(u => u.Email == viewModel.Email && u.Id != userId);

            if (existingUserWithEmail != null)
            {
                ModelState.AddModelError("Email", "Email is already registered.");
                var reloadedViewModel = await PopulateEditAccountViewModel(userId, userAccount);
                return View(reloadedViewModel);
            }

            if (existingUserWithUsername != null)
            {
                ModelState.AddModelError("UserName", "Username is already taken.");
                var reloadedViewModel = await PopulateEditAccountViewModel(userId, userAccount);
                return View(reloadedViewModel);
            }

            bool hasChanges = false;

            // Update basic fields
            if (userAccount.FirstName != viewModel.FirstName)
            {
                userAccount.FirstName = viewModel.FirstName;
                hasChanges = true;
            }

            if (userAccount.LastName != viewModel.LastName)
            {
                userAccount.LastName = viewModel.LastName;
                hasChanges = true;
            }

            if (userAccount.Bio != viewModel.Bio)
            {
                userAccount.Bio = viewModel.Bio;
                hasChanges = true;
            }

            if (userAccount.ExperienceLevel != viewModel.ExperienceLevel)
            {
                userAccount.ExperienceLevel = viewModel.ExperienceLevel;
                hasChanges = true;
            }

            // Handle photo upload
            if (PhotoFile != null && PhotoFile.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var fileExtension = Path.GetExtension(PhotoFile.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    ModelState.AddModelError("PhotoFile", "Please upload a valid image file (jpg, jpeg, png, gif).");
                    var reloadedViewModel = await PopulateEditAccountViewModel(userId, userAccount);
                    return View(reloadedViewModel);
                }

                if (PhotoFile.Length > 10 * 1024 * 1024)
                {
                    ModelState.AddModelError("PhotoFile", "The image file size should not exceed 10 MB.");
                    var reloadedViewModel = await PopulateEditAccountViewModel(userId, userAccount);
                    return View(reloadedViewModel);
                }

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
                            // Ignore file deletion errors
                        }
                    }
                }

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await PhotoFile.CopyToAsync(stream);
                }

                userAccount.Photo = $"/uploads/profiles/{fileName}";
                hasChanges = true;
            }

            // Update email if changed
            if (userAccount.Email != viewModel.Email)
            {
                var emailResult = await _userManager.SetEmailAsync(userAccount, viewModel.Email);
                if (!emailResult.Succeeded)
                {
                    foreach (var error in emailResult.Errors)
                    {
                        ModelState.AddModelError("Email", error.Description);
                    }
                    var reloadedViewModel = await PopulateEditAccountViewModel(userId, userAccount);
                    return View(reloadedViewModel);
                }
                hasChanges = true;
            }

            // Update username if changed
            if (userAccount.UserName != viewModel.UserName)
            {
                var usernameResult = await _userManager.SetUserNameAsync(userAccount, viewModel.UserName);
                if (!usernameResult.Succeeded)
                {
                    foreach (var error in usernameResult.Errors)
                    {
                        ModelState.AddModelError("UserName", error.Description);
                    }
                    var reloadedViewModel = await PopulateEditAccountViewModel(userId, userAccount);
                    return View(reloadedViewModel);
                }
                hasChanges = true;
            }

            if (hasChanges)
            {
                var updateResult = await _userManager.UpdateAsync(userAccount);
                if (!updateResult.Succeeded)
                {
                    foreach (var error in updateResult.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                    var reloadedViewModel = await PopulateEditAccountViewModel(userId, userAccount);
                    return View(reloadedViewModel);
                }

                await RefreshUserClaims(userAccount);
                ViewBag.Message = "Account updated successfully!";
            }
            else
            {
                ViewBag.Message = "No changes were detected.";
            }

            var finalViewModel = await PopulateEditAccountViewModel(userId, userAccount);
            return View(finalViewModel);
        }

        // Portfolio Management Actions
        [HttpGet]
        public async Task<IActionResult> ManagePortfolio()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var portfolios = await dbContext.Portfolios
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.Title)
                .ToListAsync();

            var userAccount = await dbContext.UserAccounts.FindAsync(userId);
            ViewBag.UserName = userAccount?.FirstName + " " + userAccount?.LastName;

            return View(portfolios);
        }

        [HttpPost]
        public async Task<IActionResult> AddPortfolio(PortfolioItem portfolioItem)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Invalid portfolio data" });
            }

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Json(new { success = false, message = "Unauthorized" });

            try
            {
                var portfolio = new Portfolio
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Title = portfolioItem.Title,
                    Description = portfolioItem.Description,
                    ProjectLink = portfolioItem.RepositoryLink
                };

                // Handle file uploads if any
                if (portfolioItem.Files != null && portfolioItem.Files.Any())
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "portfolios");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var filePaths = new List<string>();
                    foreach (var file in portfolioItem.Files)
                    {
                        if (file.Length > 0)
                        {
                            var fileName = GenerateUniqueFileName(file.FileName, uploadsFolder);
                            var filePath = Path.Combine(uploadsFolder, fileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }

                            filePaths.Add($"/uploads/portfolios/{fileName}");
                        }
                    }

                    portfolio.ProjectImages = System.Text.Json.JsonSerializer.Serialize(filePaths);
                }

                dbContext.Portfolios.Add(portfolio);
                await dbContext.SaveChangesAsync();

                return Json(new { success = true, message = "Portfolio added successfully", portfolioId = portfolio.Id });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error adding portfolio: " + ex.Message });
            }
        }

        [HttpDelete]
        public async Task<IActionResult> DeletePortfolio(Guid portfolioId)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Json(new { success = false, message = "Unauthorized" });

            try
            {
                var portfolio = await dbContext.Portfolios
                    .FirstOrDefaultAsync(p => p.Id == portfolioId && p.UserId == userId);

                if (portfolio == null)
                    return Json(new { success = false, message = "Portfolio not found" });

                // Delete associated files
                if (!string.IsNullOrEmpty(portfolio.ProjectImages))
                {
                    try
                    {
                        var filePaths = System.Text.Json.JsonSerializer.Deserialize<List<string>>(portfolio.ProjectImages);
                        if (filePaths != null)
                        {
                            foreach (var filePath in filePaths)
                            {
                                var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", filePath.TrimStart('/'));
                                if (System.IO.File.Exists(fullPath))
                                {
                                    System.IO.File.Delete(fullPath);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore file deletion errors
                    }
                }

                dbContext.Portfolios.Remove(portfolio);
                await dbContext.SaveChangesAsync();

                return Json(new { success = true, message = "Portfolio deleted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error deleting portfolio: " + ex.Message });
            }
        }
        private async Task<EditAccount> PopulateEditAccountViewModel(string userId, UserAccount userAccount)
        {
            if (userAccount == null)
                userAccount = await _userManager.FindByIdAsync(userId);

            var savedSkills = await dbContext.UserAccountSkills
                .Where(uas => uas.UserAccountId == userId)
                .Include(uas => uas.UserSkill)
                .Select(uas => uas.UserSkill)
                .OrderBy(s => s.Name)
                .ToListAsync();

            var completedAsMentor = await dbContext.MentorshipMatches
                .AnyAsync(mm => mm.MentorId == userId && mm.Status == "Completed");

            var completedAsMentee = await dbContext.MentorshipMatches
                .AnyAsync(mm => mm.MenteeId == userId && mm.Status == "Completed");

            return new EditAccount
            {
                UserId = Guid.Parse(userId),
                FirstName = userAccount.FirstName ?? string.Empty,
                LastName = userAccount.LastName ?? string.Empty,
                Email = userAccount.Email ?? string.Empty,
                UserName = userAccount.UserName ?? string.Empty,
                Photo = userAccount.Photo ?? string.Empty,
                Bio = userAccount.Bio ?? string.Empty,
                ExperienceLevel = userAccount.ExperienceLevel ?? string.Empty,
                SavedSkills = savedSkills,
                TotalSkillsCount = savedSkills.Count,
                HasCompletedMentorshipAsMentor = completedAsMentor,
                HasCompletedMentorshipAsMentee = completedAsMentee
            };
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
                var confirmationLink = Url.Action("ConfirmEmailChange", "Freelancer",
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
