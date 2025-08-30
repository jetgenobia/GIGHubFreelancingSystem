using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Freelancing.Data;
using Freelancing.Models;
using Freelancing.Models.Entities;
using System.Security.Claims;
using System.Text.Json;
using Freelancing.Services;

namespace Freelancing.Controllers
{
    [Authorize(Roles = "Client,Freelancer")]
    public class DeliverableController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly INotificationService _notificationService;

        public DeliverableController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment, INotificationService notificationService)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Index(Guid id)
        {
            // Get current user information
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized();

            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var isClient = userRole == "Client";
            var isFreelancer = userRole == "Freelancer";

            // Fetch contract with related data
            var contract = await _context.Contracts
                .Include(c => c.Project)
                    .ThenInclude(p => p.User) // Client
                .Include(c => c.Bidding)
                    .ThenInclude(b => b.User) // Freelancer
                .FirstOrDefaultAsync(c => c.Id == id);

            if (contract == null)
                return NotFound("Contract not found.");

            // Verify user has access to this contract
            if (isClient && contract.Project.UserId != currentUserId)
                return Forbid("You don't have access to this contract.");

            if (isFreelancer && contract.Bidding.UserId != currentUserId)
                return Forbid("You don't have access to this contract.");

            // Fetch existing deliverables
            var deliverables = await _context.Deliverables
                .Include(d => d.SubmittedByUser)
                .Include(d => d.ReviewedByUser)
                .Where(d => d.ContractId == id)
                .OrderByDescending(d => d.SubmittedAt)
                .ToListAsync();

            ViewBag.ContractId = id;
            ViewBag.Contract = contract;
            ViewBag.Deliverables = deliverables;
            ViewBag.IsClient = isClient;
            ViewBag.IsFreelancer = isFreelancer;
            ViewBag.CurrentUserId = currentUserId;

            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Freelancer")]
        public async Task<IActionResult> Submit(SubmitDeliverableViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please correct the errors below.";
                return RedirectToAction("Index", new { id = model.ContractId });
            }

            try
            {
                // Get current user
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(currentUserId))
                    return Unauthorized();

                // Verify contract exists and user is the freelancer
                var contract = await _context.Contracts
                    .Include(c => c.Bidding)
                    .Include(c => c.Project)
                        .ThenInclude(p => p.User) // Client
                    .FirstOrDefaultAsync(c => c.Id == model.ContractId);

                if (contract == null)
                    return NotFound("Contract not found.");

                if (contract.Bidding.UserId != currentUserId)
                    return Forbid("You can only submit deliverables for your own contracts.");

                // Handle file uploads
                var uploadedFilePaths = new List<string>();
                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "deliverables");

                // Create directory if it doesn't exist
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                if (model.SubmittedFiles != null && model.SubmittedFiles.Count > 0)
                {
                    foreach (var file in model.SubmittedFiles)
                    {
                        if (file.Length > 0)
                        {
                            // Validate file size (10MB limit)
                            if (file.Length > 10 * 1024 * 1024)
                            {
                                ModelState.AddModelError("SubmittedFiles", $"File {file.FileName} is too large. Maximum size is 10MB.");
                                continue;
                            }

                            // Generate unique filename
                            var fileName = GenerateUniqueFileName(file.FileName, uploadsFolder);
                            var filePath = Path.Combine(uploadsFolder, fileName);

                            // Save file
                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }

                            // Store relative path
                            uploadedFilePaths.Add($"/uploads/deliverables/{fileName}");
                        }
                    }
                }

                // Create deliverable
                var deliverable = new Deliverable
                {
                    Id = Guid.NewGuid(),
                    ContractId = model.ContractId,
                    SubmittedByUserId = currentUserId,
                    Title = model.Title,
                    Status = "Submitted",
                    SubmittedFilesPaths = uploadedFilePaths.Count > 0 ? JsonSerializer.Serialize(uploadedFilePaths) : null,
                    RepositoryLinks = model.RepositoryLinks,
                    SubmittedAt = DateTime.UtcNow.ToLocalTime()
                };

                // Add to database
                _context.Deliverables.Add(deliverable);
                await _context.SaveChangesAsync();

                // Send notification to client
                var notification = new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = contract.Project.UserId, // Client's ID
                    Title = "New Deliverable Submitted",
                    Message = $"A new deliverable '{model.Title}' has been submitted for project '{contract.Project.ProjectName}'. Please review it.",
                    Type = "Deliverable",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow.ToLocalTime(),
                    RelatedUrl = $"/Deliverable/Index/{model.ContractId}",
                    IconSvg = "<svg viewBox=\"0 0 24 24\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\"><g id=\"SVGRepo_bgCarrier\" stroke-width=\"0\"></g><g id=\"SVGRepo_tracerCarrier\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></g><g id=\"SVGRepo_iconCarrier\"> <path d=\"M13 3H8.2C7.0799 3 6.51984 3 6.09202 3.21799C5.71569 3.40973 5.40973 3.71569 5.21799 4.09202C5 4.51984 5 5.0799 5 6.2V17.8C5 18.9201 5 19.4802 5.21799 19.908C5.40973 20.2843 5.71569 20.5903 6.09202 20.782C6.51984 21 7.0799 21 8.2 21H12M13 3L19 9M13 3V7.4C13 7.96005 13 8.24008 13.109 8.45399C13.2049 8.64215 13.3578 8.79513 13.546 8.89101C13.7599 9 14.0399 9 14.6 9H19M19 9V12M17 19H21M19 17V21\" stroke=\"#000000\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></path> </g></svg>"
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                TempData["Message"] = "Deliverable submitted successfully!";
                return RedirectToAction("Index", new { id = model.ContractId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while submitting the deliverable. Please try again.";
                return RedirectToAction("Index", new { id = model.ContractId });
            }
        }

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

        [HttpPost]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> Approve(Guid deliverableId, string? reviewComments = null)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(currentUserId))
                    return Unauthorized();

                var deliverable = await _context.Deliverables
                    .Include(d => d.Contract)
                        .ThenInclude(c => c.Project)
                    .Include(d => d.Contract)
                        .ThenInclude(c => c.Bidding)
                            .ThenInclude(b => b.User)
                    .FirstOrDefaultAsync(d => d.Id == deliverableId);

                if (deliverable == null)
                    return NotFound("Deliverable not found.");

                // Verify the current user is the client of this project
                if (deliverable.Contract.Project.UserId != currentUserId)
                    return Forbid("You can only approve deliverables for your own projects.");

                // Update deliverable status
                deliverable.Status = "Approved";
                deliverable.ReviewedAt = DateTime.UtcNow.ToLocalTime();
                deliverable.ReviewedByUserId = currentUserId;
                deliverable.ReviewComments = reviewComments;

                await _context.SaveChangesAsync();

                // Send notification to freelancer
                var notification = new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = deliverable.Contract.Bidding.UserId, // Freelancer's ID
                    Title = "Deliverable Approved",
                    Message = $"Your deliverable '{deliverable.Title}' has been approved for project '{deliverable.Contract.Project.ProjectName}'.",
                    Type = "Deliverable",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow.ToLocalTime(),
                    RelatedUrl = $"/Deliverable/Index/{deliverable.ContractId}",
                    IconSvg = "<svg viewBox=\"0 0 24 24\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\"><g id=\"SVGRepo_bgCarrier\" stroke-width=\"0\"></g><g id=\"SVGRepo_tracerCarrier\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></g><g id=\"SVGRepo_iconCarrier\"> <path d=\"M15 19L17 21L21 17M13 3H8.2C7.0799 3 6.51984 3 6.09202 3.21799C5.71569 3.40973 5.40973 3.71569 5.21799 4.09202C5 4.51984 5 5.0799 5 6.2V17.8C5 18.9201 5 19.4802 5.21799 19.908C5.40973 20.2843 5.71569 20.5903 6.09202 20.782C6.51984 21 7.0799 21 8.2 21H12M13 3L19 9M13 3V7.4C13 7.96005 13 8.24008 13.109 8.45399C13.2049 8.64215 13.3578 8.79513 13.546 8.89101C13.7599 9 14.0399 9 14.6 9H19M19 9V13.5\" stroke=\"#000000\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></path> </g></svg>"
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                TempData["Message"] = "Deliverable approved successfully!";
                return RedirectToAction("Index", new { id = deliverable.ContractId });
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred while approving the deliverable. Please try again.";
                return RedirectToAction("Index", new { id = deliverableId });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> RequestRevision(Guid deliverableId, string reviewComments)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(currentUserId))
                    return Unauthorized();

                var deliverable = await _context.Deliverables
                    .Include(d => d.Contract)
                        .ThenInclude(c => c.Project)
                    .Include(d => d.Contract)
                        .ThenInclude(c => c.Bidding)
                            .ThenInclude(b => b.User)
                    .FirstOrDefaultAsync(d => d.Id == deliverableId);

                if (deliverable == null)
                    return NotFound("Deliverable not found.");

                // Verify the current user is the client of this project
                if (deliverable.Contract.Project.UserId != currentUserId)
                    return Forbid("You can only request revisions for deliverables in your own projects.");

                if (string.IsNullOrWhiteSpace(reviewComments))
                {
                    TempData["ErrorMessage"] = "Please provide revision comments.";
                    return RedirectToAction("Index", new { id = deliverable.ContractId });
                }

                // Update deliverable status
                deliverable.Status = "For Revision";
                deliverable.ReviewedAt = DateTime.UtcNow.ToLocalTime();
                deliverable.ReviewedByUserId = currentUserId;
                deliverable.ReviewComments = reviewComments;

                await _context.SaveChangesAsync();

                // Send notification to freelancer
                var notification = new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = deliverable.Contract.Bidding.UserId, // Freelancer's ID
                    Title = "Deliverable Revision Requested",
                    Message = $"Your deliverable '{deliverable.Title}' needs revision for project '{deliverable.Contract.Project.ProjectName}'. Please check the review comments.",
                    Type = "Deliverable",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow.ToLocalTime(),
                    RelatedUrl = $"/Deliverable/Index/{deliverable.ContractId}",
                    IconSvg = "<svg viewBox=\"0 0 24 24\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\"><g id=\"SVGRepo_bgCarrier\" stroke-width=\"0\"></g><g id=\"SVGRepo_tracerCarrier\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></g><g id=\"SVGRepo_iconCarrier\"> <path d=\"M13 3H8.2C7.0799 3 6.51984 3 6.09202 3.21799C5.71569 3.40973 5.40973 3.71569 5.21799 4.09202C5 4.51984 5 5.0799 5 6.2V17.8C5 18.9201 5 19.4802 5.21799 19.908C5.40973 20.2843 5.71569 20.5903 6.09202 20.782C6.51984 21 7.0799 21 8.2 21H13M13 3L19 9M13 3V7.4C13 7.96005 13 8.24008 13.109 8.45399C13.2049 8.64215 13.3578 8.79513 13.546 8.89101C13.7599 9 14.0399 9 14.6 9H19M19 9V11.0228M21 17H15M15 17L17 19M15 17L17 15\" stroke=\"#000000\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></path> </g></svg>"
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                TempData["Message"] = "Revision requested successfully!";
                return RedirectToAction("Index", new { id = deliverable.ContractId });
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred while requesting revision. Please try again.";
                return RedirectToAction("Index", new { id = deliverableId });
            }
        }
    }
}
