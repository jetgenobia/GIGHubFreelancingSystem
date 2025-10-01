using Freelancing.Data;
using Freelancing.Models.Entities;
using Freelancing.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Freelancing.Controllers
{
    [Authorize(Roles = "Admin")]
    public class Admin : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IIdentityEncryptionService _encryptionService;
        private readonly IIdentityVerificationService _verificationService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<Admin> _logger;
        private readonly IGoogleCloudStorageService _googleCloudStorageService;

        public Admin(
            ApplicationDbContext context,
            IIdentityEncryptionService encryptionService,
            IIdentityVerificationService verificationService,
            INotificationService notificationService,
            IGoogleCloudStorageService googleCloudStorageService,
            ILogger<Admin> logger)
        {
            _context = context;
            _encryptionService = encryptionService;
            _verificationService = verificationService;
            _notificationService = notificationService;
            _googleCloudStorageService = googleCloudStorageService;
            _logger = logger;
        }

        /*[HttpGet("admin/setup-cors")]
        public async Task<IActionResult> SetupCors()
        {
            try
            {
                await _googleCloudStorageService.SetBucketCorsAsync();
                return Ok("CORS configured successfully!");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error: {ex.Message}");
            }
        }*/
        public IActionResult Reports()
        {
            return View();
        }

        // GET: Admin/IdentityVerification
        public async Task<IActionResult> IdentityVerification()
        {
            var pending = await _context.IdentityVerifications
                .Where(v => v.Status == "PENDING")
                .Include(v => v.UserAccount)
                .OrderBy(v => v.CreatedAt)
                .Select(v => new VerificationAdminViewModel
                {
                    Id = v.Id,
                    UserId = v.UserAccountId,
                    FullName = v.UserAccount != null ? (v.UserAccount.FirstName + " " + v.UserAccount.LastName).Trim() : "",
                    Email = v.UserAccount != null ? v.UserAccount.Email : "",
                    Status = v.Status,
                    CreatedAt = v.CreatedAt,
                    IdDocumentVerified = v.IdDocumentVerified,
                    IdDocumentConfidence = v.IdDocumentConfidence,
                    FaceVerified = v.FaceVerified,
                    FaceConfidence = v.FaceConfidence
                })
                .ToListAsync();

            return View(pending);
        }

        // Accept both route segment and querystring forms.
        // GET: /Admin/ViewDocument/{id}  OR /Admin/ViewDocument?id={id}
        [HttpGet("Admin/ViewDocument/{id:guid}")]
        [HttpGet("Admin/ViewDocument")]
        public async Task<IActionResult> ViewDocument(Guid id)
        {
            try
            {
                if (id == Guid.Empty)
                    return BadRequest();

                var verification = await _context.IdentityVerifications
                    .FirstOrDefaultAsync(v => v.Id == id);

                if (verification == null || verification.EncryptedIdDocumentImage == null)
                    return NotFound();

                // Decrypt bytes (expects decrypt method on your encryption service)
                var decrypted = _encryptionService.DecryptDocumentImage(verification.EncryptedIdDocumentImage, verification.UserAccountId);
                if (decrypted == null || decrypted.Length == 0)
                    return NotFound();

                // If you store content-type, replace with that; default to jpeg
                return File(decrypted, "image/jpeg");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error returning document image for verification {VerificationId}", id);
                return StatusCode(500);
            }
        }

        // Accept both route segment and querystring forms.
        // GET: /Admin/ViewFace/{id}  OR /Admin/ViewFace?id={id}
        [HttpGet("Admin/ViewFace/{id:guid}")]
        [HttpGet("Admin/ViewFace")]
        public async Task<IActionResult> ViewFace(Guid id)
        {
            try
            {
                if (id == Guid.Empty)
                    return BadRequest();

                var verification = await _context.IdentityVerifications
                    .FirstOrDefaultAsync(v => v.Id == id);

                if (verification == null || verification.EncryptedFaceImage == null)
                    return NotFound();

                var decrypted = _encryptionService.DecryptDocumentImage(verification.EncryptedFaceImage, verification.UserAccountId);
                if (decrypted == null || decrypted.Length == 0)
                    return NotFound();

                return File(decrypted, "image/jpeg");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error returning face image for verification {VerificationId}", id);
                return StatusCode(500);
            }
        }

        // POST: Admin/Approve (expects JSON { "id": "...Guid..." })
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve([FromBody] VerificationActionRequest request)
        {
            if (request == null || request.Id == Guid.Empty)
                return Json(new { success = false, message = "Invalid request" });

            try
            {
                var ok = await _verificationService.UpdateVerificationStatusAsync(request.Id, "APPROVED");
                if (!ok)
                    return Json(new { success = false, message = "Unable to approve verification" });

                var verification = await _context.IdentityVerifications.FirstOrDefaultAsync(v => v.Id == request.Id);
                if (verification != null)
                {
                    var userId = verification.UserAccountId;
                    await _notification_service_CreateApprovedNotification(userId);
                }

                return Json(new { success = true, message = "Verification approved" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving verification {VerificationId}", request.Id);
                return Json(new { success = false, message = "Error approving verification" });
            }
        }

        // POST: Admin/Reject (expects JSON { "id": "...", "reason": "..." })
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject([FromBody] VerificationActionRequest request)
        {
            if (request == null || request.Id == Guid.Empty)
                return Json(new { success = false, message = "Invalid request" });

            try
            {
                var reason = (request.Reason ?? "").Trim();
                var ok = await _verificationService.UpdateVerificationStatusAsync(request.Id, "REJECTED", reason);
                if (!ok)
                    return Json(new { success = false, message = "Unable to reject verification" });

                // Get the verification to create notification
                var verification = await _context.IdentityVerifications.FirstOrDefaultAsync(v => v.Id == request.Id);
                if (verification != null)
                {
                    var userId = verification.UserAccountId;
                    await _notification_service_CreateRejectedNotification(userId, reason);
                }

                return Json(new { success = true, message = "Verification rejected" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting verification {VerificationId}", request.Id);
                return Json(new { success = false, message = "Error rejecting verification" });
            }
        }

        private async Task _notification_service_CreateRejectedNotification(string userId, string reason)
        {
            var message = string.IsNullOrWhiteSpace(reason)
                ? "Your identity verification has been rejected. Please review and resubmit your documents."
                : $"Your identity verification has been rejected. Please review and resubmit your documents.";

            await _notificationService.CreateNotificationAsync(
                userId,
                "Identity Verification Rejected",
                message,
                "identity_rejected",
                "<svg viewBox=\"0 0 24 24\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\"><g id=\"SVGRepo_bgCarrier\" stroke-width=\"0\"></g><g id=\"SVGRepo_tracerCarrier\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></g><g id=\"SVGRepo_iconCarrier\"> <path fill-rule=\"evenodd\" clip-rule=\"evenodd\" d=\"M10 1C9.73478 1 9.48043 1.10536 9.29289 1.29289L3.29289 7.29289C3.10536 7.48043 3 7.73478 3 8V20C3 21.6569 4.34315 23 6 23H18C19.6569 23 21 21.6569 21 20V4C21 2.34315 19.6569 1 18 1H10ZM11 3H18C18.5523 3 19 3.44772 19 4V20C19 20.5523 18.5523 21 18 21H6C5.44772 21 5 20.5523 5 20V9H10C10.5523 9 11 8.55228 11 8V3ZM9 7H6.41421L9 4.41421V7ZM12 13.5858L9.70711 11.2929C9.31658 10.9024 8.68342 10.9024 8.29289 11.2929C7.90237 11.6834 7.90237 12.3166 8.29289 12.7071L10.5858 15L8.29289 17.2929C7.90237 17.6834 7.90237 18.3166 8.29289 18.7071C8.68342 19.0976 9.31658 19.0976 9.70711 18.7071L12 16.4142L14.2929 18.7071C14.6834 19.0976 15.3166 19.0976 15.7071 18.7071C16.0976 18.3166 16.0976 17.6834 15.7071 17.2929L13.4142 15L15.7071 12.7071C16.0976 12.3166 16.0976 11.6834 15.7071 11.2929C15.3166 10.9024 14.6834 10.9024 14.2929 11.2929L12 13.5858Z\" fill=\"#000000\"></path> </g></svg>",
                "/IdentityVerification/Status"
            );
        }

        private async Task _notification_service_CreateApprovedNotification(string userId)
        {
            await _notificationService.CreateNotificationAsync(
                userId,
                "Identity Verification Approved",
                "Your identity verification has been approved by an administrator. You can now access verified features.",
                "identity_approved",
                "<svg viewBox=\"0 0 24 24\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\"><g id=\"SVGRepo_bgCarrier\" stroke-width=\"0\"></g><g id=\"SVGRepo_tracerCarrier\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></g><g id=\"SVGRepo_iconCarrier\"> <path fill-rule=\"evenodd\" clip-rule=\"evenodd\" d=\"M10 1C9.73478 1 9.48043 1.10536 9.29289 1.29289L3.29289 7.29289C3.10536 7.48043 3 7.73478 3 8V20C3 21.6569 4.34315 23 6 23H18C19.6569 23 21 21.6569 21 20V4C21 2.34315 19.6569 1 18 1H10ZM11 3H18C18.5523 3 19 3.44772 19 4V20C19 20.5523 18.5523 21 18 21H6C5.44772 21 5 20.5523 5 20V9H10C10.5523 9 11 8.55228 11 8V3ZM9 7H6.41421L9 4.41421V7ZM16.7682 12.6402C17.1218 12.2159 17.0645 11.5853 16.6402 11.2318C16.2159 10.8782 15.5853 10.9355 15.2318 11.3598L10.9328 16.5186L8.70711 14.2929C8.31658 13.9024 7.68342 13.9024 7.29289 14.2929C6.90237 14.6834 6.90237 15.3166 7.29289 15.7071L10.2929 18.7071C10.4916 18.9058 10.7646 19.0117 11.0453 18.999C11.326 18.9862 11.5884 18.856 11.7682 18.6402L16.7682 12.6402Z\" fill=\"#000000\"></path> </g></svg>",
                "/IdentityVerification/Status"
            );
        }

        // View model used by the admin list view
        public class VerificationAdminViewModel
        {
            public Guid Id { get; set; }
            public string UserId { get; set; } = "";
            public string FullName { get; set; } = "";
            public string Email { get; set; } = "";
            public string Status { get; set; } = "";
            public DateTime? CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; }
            public bool? IdDocumentVerified { get; set; }
            public float? IdDocumentConfidence { get; set; }
            public bool? FaceVerified { get; set; }
            public float? FaceConfidence { get; set; }
        }

        // Request DTO for approve/reject endpoints
        public class VerificationActionRequest
        {
            public Guid Id { get; set; }
            public string? Reason { get; set; }
        }
    }
}