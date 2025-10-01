using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Freelancing.Data;
using Freelancing.Models;
using Freelancing.Models.Entities;
using System.Security.Claims;
using System.Text.Json;
using Freelancing.Services;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Freelancing.Controllers
{
    [Authorize(Roles = "Client,Freelancer")]
    public class DeliverableController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly IGoogleCloudStorageService _googleCloudStorageService;

        public DeliverableController(ApplicationDbContext context, INotificationService notificationService, IGoogleCloudStorageService googleCloudStorageService)
        {
            _context = context;
            _notificationService = notificationService;
            _googleCloudStorageService = googleCloudStorageService;
        }

        public async Task<IActionResult> Index(Guid id, string? status = null)
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

            var storedStatuses = new[] { "Submitted", "Approved", "For Revision" };
            bool hasDeadline = contract.Project?.Deadline != null;

            if (!string.IsNullOrWhiteSpace(status) && status != "All")
            {
                if (status == "Late")
                {
                    if (hasDeadline)
                    {
                        var deadline = contract.Project!.Deadline!.Value;
                        deliverables = deliverables
                            .Where(d => d.SubmittedAt > deadline)
                            .ToList();
                    }
                    else
                    {
                        deliverables = new List<Deliverable>();
                    }
                }
                else
                {
                    deliverables = deliverables
                        .Where(d => d.Status == status)
                        .ToList();
                }
            }

            var statusOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "All", Text = "All" }
            };
            statusOptions.AddRange(storedStatuses.Select(s => new SelectListItem { Value = s, Text = s }));

            // Only show Late option if deadline exists
            if (hasDeadline)
                statusOptions.Add(new SelectListItem { Value = "Late", Text = "Late" });

            ViewBag.StatusOptions = statusOptions;
            ViewBag.CurrentStatus = string.IsNullOrWhiteSpace(status) ? "All" : status;

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

                // Handle file uploads using Google Cloud Storage
                var uploadedFilePaths = new List<string>();

                if (model.SubmittedFiles != null && model.SubmittedFiles.Count > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".svg", ".pdf", ".doc", ".docx", ".txt", ".zip", ".mp4", ".mov", ".avi" };
                    const int maxFileSize = 10 * 1024 * 1024; // 10MB

                    foreach (var file in model.SubmittedFiles)
                    {
                        if (file.Length > 0)
                        {
                            // Validate file type
                            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                            if (!allowedExtensions.Contains(fileExtension))
                            {
                                ModelState.AddModelError("SubmittedFiles", $"File {file.FileName} is not a valid file type. Allowed types: {string.Join(", ", allowedExtensions)}");
                                TempData["ErrorMessage"] = "One or more files have invalid file types.";
                                return RedirectToAction("Index", new { id = model.ContractId });
                            }

                            // Validate file size
                            if (file.Length > maxFileSize)
                            {
                                ModelState.AddModelError("SubmittedFiles", $"File {file.FileName} is too large. Maximum size is 10MB.");
                                TempData["ErrorMessage"] = "One or more files exceed the maximum size limit.";
                                return RedirectToAction("Index", new { id = model.ContractId });
                            }

                            try
                            {
                                // Upload file to Google Cloud Storage
                                var publicUrl = await _googleCloudStorageService.UploadFileAsync(file, "deliverables");
                                uploadedFilePaths.Add(publicUrl);
                            }
                            catch (Exception ex)
                            {
                                ModelState.AddModelError("SubmittedFiles", $"Failed to upload {file.FileName}: {ex.Message}");
                                TempData["ErrorMessage"] = $"Failed to upload {file.FileName}. Please try again.";
                                return RedirectToAction("Index", new { id = model.ContractId });
                            }
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
                    SubmittedAt = DateTime.UtcNow
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
                    CreatedAt = DateTime.UtcNow,
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
                deliverable.ReviewedAt = DateTime.UtcNow;
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
                    CreatedAt = DateTime.UtcNow,
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
                deliverable.ReviewedAt = DateTime.UtcNow;
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
                    CreatedAt = DateTime.UtcNow,
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