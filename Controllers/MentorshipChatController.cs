using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Freelancing.Data;
using Freelancing.Models.Entities;
using Freelancing.Models;
using Freelancing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Freelancing.Controllers
{
    [Authorize]
    public class MentorshipChatController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IMessageEncryptionService _encryptionService;
        private const int MaxFileSize = 10 * 1024 * 1024; // 10MB
        private readonly string[] AllowedFileTypes = { ".pdf", ".doc", ".docx", ".txt", ".jpg", ".jpeg", ".png", ".gif", ".mp4", ".mov", ".avi" };

        public MentorshipChatController(ApplicationDbContext context, IWebHostEnvironment environment, IMessageEncryptionService encryptionService)
        {
            _context = context;
            _environment = environment;
            _encryptionService = encryptionService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(Guid matchId)
        {
            var userId = GetCurrentUserId();

            // Verify user has access to this mentorship match
            var match = await _context.MentorshipMatches
                .Include(mm => mm.Mentor)
                .Include(mm => mm.Mentee)
                .FirstOrDefaultAsync(mm => mm.Id == matchId &&
                                          (mm.MentorId == userId || mm.MenteeId == userId) &&
                                          (mm.Status == "Active" || mm.Status == "Completed"));

            if (match == null)
            {
                TempData["Error"] = "Access denied or mentorship not found";
                return RedirectToAction("AvailableMentors", "MentorshipMatching");
            }

            // Get and decrypt chat messages
            var encryptionKey = _encryptionService.GenerateRoomKey(matchId.ToString());
            var messages = await GetDecryptedMessages(matchId, userId, encryptionKey);

            var status = match.Status;
            var partner = match.MentorId == userId ? match.Mentee : match.Mentor;

            var viewModel = new MentorshipChatViewModel
            {
                MatchId = matchId,
                Status = status,
                Partner = partner,
                CurrentUserId = userId,
                Messages = messages,
                IsCurrentUserMentor = match.MentorId == userId
            };

            // Mark messages as read
            await MarkMessagesAsRead(matchId, userId);

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> GetMessages(Guid matchId, int page = 1, int pageSize = 50)
        {
            var userId = GetCurrentUserId();

            // Verify access
            var hasAccess = await _context.MentorshipMatches
                .AnyAsync(mm => mm.Id == matchId &&
                               (mm.MentorId == userId || mm.MenteeId == userId) &&
                               (mm.Status == "Active" || mm.Status == "Completed"));

            if (!hasAccess)
            {
                return Json(new { success = false, message = "Access denied" });
            }

            // Get and decrypt messages
            var encryptionKey = _encryptionService.GenerateRoomKey(matchId.ToString());
            var messages = await GetDecryptedMessagesPage(matchId, userId, encryptionKey, page, pageSize);

            return Json(new { success = true, messages = messages.OrderBy(m => m.SentAt) });
        }

        private async Task<List<ChatMessageViewModel>> GetDecryptedMessages(Guid matchId, string userId, string encryptionKey)
        {
            var messages = await _context.MentorshipChatMessages
                .Where(mcm => mcm.MentorshipMatchId == matchId && !mcm.IsDeleted)
                .Include(mcm => mcm.Sender)
                .OrderBy(mcm => mcm.SentAt)
                .ToListAsync();

            var decryptedMessages = new List<ChatMessageViewModel>();

            foreach (var message in messages)
            {
                var viewModel = new ChatMessageViewModel
                {
                    Id = message.Id,
                    SenderId = message.SenderId,
                    SenderName = $"{message.Sender.FirstName} {message.Sender.LastName}",
                    MessageType = message.MessageType,
                    FileUrl = message.FileUrl,
                    FileType = message.FileType,
                    FileSize = message.FileSize,
                    SentAt = message.SentAt,
                    IsRead = message.IsRead,
                    IsCurrentUser = message.SenderId == userId
                };

                // Try to decrypt the message
                try
                {
                    viewModel.Message = _encryptionService.DecryptMessage(message.Message, encryptionKey);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Decryption failed for message {message.Id}: {ex.Message}");
                    // If decryption fails, assume it's a plain text message (for backward compatibility)
                    viewModel.Message = message.Message;
                }

                decryptedMessages.Add(viewModel);
            }

            return decryptedMessages;
        }

        private async Task<List<dynamic>> GetDecryptedMessagesPage(Guid matchId, string userId, string encryptionKey, int page, int pageSize)
        {
            var messages = await _context.MentorshipChatMessages
                .Where(mcm => mcm.MentorshipMatchId == matchId && !mcm.IsDeleted)
                .Include(mcm => mcm.Sender)
                .OrderByDescending(mcm => mcm.SentAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var decryptedMessages = new List<dynamic>();

            foreach (var message in messages)
            {
                string decryptedContent;
                try
                {
                    decryptedContent = _encryptionService.DecryptMessage(message.Message, encryptionKey);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Decryption failed for message {message.Id}: {ex.Message}");
                    // Fallback to original message if decryption fails
                    decryptedContent = message.Message;
                }

                decryptedMessages.Add(new
                {
                    Id = message.Id,
                    SenderId = message.SenderId,
                    SenderName = $"{message.Sender.FirstName} {message.Sender.LastName}",
                    Message = decryptedContent,
                    MessageType = message.MessageType,
                    FileUrl = message.FileUrl,
                    FileType = message.FileType,
                    FileSize = message.FileSize,
                    SentAt = message.SentAt,
                    IsRead = message.IsRead,
                    IsCurrentUser = message.SenderId == userId
                });
            }

            return decryptedMessages;
        }

        [HttpPost]
        public async Task<IActionResult> UploadFile(Guid matchId, IFormFile file)
        {
            try
            {
                var userId = GetCurrentUserId();

                // Verify access
                var hasAccess = await _context.MentorshipMatches
                    .AnyAsync(mm => mm.Id == matchId &&
                                   (mm.MentorId == userId || mm.MenteeId == userId) &&
                                   (mm.Status == "Active" || mm.Status == "Completed"));

                if (!hasAccess)
                {
                    return Json(new { success = false, message = "Access denied" });
                }

                if (file == null || file.Length == 0)
                {
                    return Json(new { success = false, message = "No file selected" });
                }

                if (file.Length > MaxFileSize)
                {
                    return Json(new { success = false, message = $"File size exceeds {MaxFileSize / 1024 / 1024}MB limit" });
                }

                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedFileTypes.Contains(extension))
                {
                    return Json(new
                    {
                        success = false,
                        message = $"File type '{extension}' not allowed. Allowed types: {string.Join(", ", AllowedFileTypes)}"
                    });
                }

                // Create upload directory if it doesn't exist
                var uploadPath = Path.Combine(_environment.WebRootPath, "uploads", "mentorship-chat");
                if (!Directory.Exists(uploadPath))
                {
                    Directory.CreateDirectory(uploadPath);
                }

                // Generate unique filename to prevent conflicts
                var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadPath, uniqueFileName);

                // Save file to disk
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Create file URL
                var fileUrl = $"/uploads/mentorship-chat/{uniqueFileName}";

                // Optional: Encrypt the original filename before storing
                var encryptionKey = _encryptionService.GenerateRoomKey(matchId.ToString());
                var encryptedFileName = file.FileName;
                try
                {
                    encryptedFileName = _encryptionService.EncryptMessage(file.FileName, encryptionKey);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to encrypt filename: {ex.Message}");
                    // Continue with unencrypted filename
                }

                Console.WriteLine($"File uploaded successfully: {file.FileName} -> {fileUrl}");

                return Json(new
                {
                    success = true,
                    fileName = file.FileName, // Return original filename for display
                    fileUrl = fileUrl,
                    fileSize = file.Length,
                    fileType = file.ContentType ?? "application/octet-stream"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Upload error: {ex.Message}");
                return Json(new { success = false, message = $"Upload failed: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteMessage(Guid messageId)
        {
            var userId = GetCurrentUserId();

            var message = await _context.MentorshipChatMessages
                .Include(mcm => mcm.MentorshipMatch)
                .FirstOrDefaultAsync(mcm => mcm.Id == messageId && mcm.SenderId == userId);

            if (message == null)
            {
                return Json(new { success = false, message = "Message not found or access denied" });
            }

            // Soft delete
            message.IsDeleted = true;
            message.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpGet]
        [Route("MentorshipChat/VideoCall/{matchId}")]
        public async Task<IActionResult> VideoCall(string matchId)
        {
            try
            {
                var userId = GetCurrentUserId();

                // Parse the matchId
                Guid parsedMatchId;
                if (string.IsNullOrEmpty(matchId) || !Guid.TryParse(matchId, out parsedMatchId))
                {
                    TempData["Error"] = "Invalid mentorship match";
                    return RedirectToAction("AvailableMentors", "MentorshipMatching");
                }

                var match = await _context.MentorshipMatches
                    .Include(mm => mm.Mentor)
                    .Include(mm => mm.Mentee)
                    .Include(mm => mm.MentorMentorship)
                    .Include(mm => mm.MenteeMentorship)
                    .FirstOrDefaultAsync(mm => mm.Id == parsedMatchId &&
                                              (mm.MentorId == userId || mm.MenteeId == userId) &&
                                              (mm.Status == "Active" || mm.Status == "Completed"));

                if (match == null)
                {
                    // Check if the match exists but user doesn't have access
                    var matchExists = await _context.MentorshipMatches
                        .AnyAsync(mm => mm.Id == parsedMatchId);
                    
                    if (!matchExists)
                    {
                        TempData["Error"] = "Mentorship match not found";
                        return RedirectToAction("AvailableMentors", "MentorshipMatching");
                    }
                    else
                    {
                        TempData["Error"] = "Access denied or mentorship not found";
                        return RedirectToAction("AvailableMentors", "MentorshipMatching");
                    }
                }
                
                var partner = match.MentorId == userId ? match.Mentee : match.Mentor;

                var viewModel = new VideoCallViewModel
                {
                    MatchId = parsedMatchId,
                    Partner = partner,
                    CurrentUserId = userId,
                    IsCurrentUserMentor = match.MentorId == userId
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "An error occurred while accessing the video call";
                return RedirectToAction("AvailableMentors", "MentorshipMatching");
            }
        }

        private async Task MarkMessagesAsRead(Guid matchId, string userId)
        {
            var unreadMessages = await _context.MentorshipChatMessages
                .Where(mcm => mcm.MentorshipMatchId == matchId &&
                             mcm.SenderId != userId && !mcm.IsRead)
                .ToListAsync();

            foreach (var message in unreadMessages)
            {
                message.IsRead = true;
                message.ReadAt = DateTime.UtcNow;
            }

            if (unreadMessages.Any())
            {
                await _context.SaveChangesAsync();
            }
        }

        private string GetCurrentUserId()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                throw new InvalidOperationException("Invalid user ID");
            }
            return userId;
        }

        // Temporary action to fix existing mentorship matches
        [HttpGet]
        public async Task<IActionResult> FixMentorshipMatches()
        {
            try
            {
                var mentorshipService = HttpContext.RequestServices.GetService<IMentorshipMatchingService>();
                if (mentorshipService != null)
                {
                    var result = await mentorshipService.FixExistingMentorshipMatchesAsync();
                    return Json(new { success = result, message = result ? "Mentorship matches fixed successfully" : "No matches needed fixing" });
                }
                return Json(new { success = false, message = "Service not available" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }


    }
}