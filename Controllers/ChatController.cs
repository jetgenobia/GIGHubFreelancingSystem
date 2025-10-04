using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Freelancing.Data;
using Freelancing.Models.Entities;
using Freelancing.Models;
using Freelancing.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;

namespace Freelancing.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IMessageEncryptionService _encryptionService;
        private readonly IGoogleCloudStorageService _googleCloudStorageService;
        private const int MaxFileSize = 10 * 1024 * 1024; // 10MB
        private readonly string[] AllowedFileTypes = { ".pdf", ".doc", ".docx", ".txt", ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".mp4", ".mov", ".avi", ".wmv", ".flv", ".webm" };

        public ChatController(ApplicationDbContext context, IWebHostEnvironment environment, IMessageEncryptionService encryptionService, IGoogleCloudStorageService googleCloudStorageService)
        {
            _context = context;
            _environment = environment;
            _encryptionService = encryptionService;
            _googleCloudStorageService = googleCloudStorageService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(Guid? chatRoomId = null, string? targetUserId = null)
        {
            var userId = GetCurrentUserId();

            // Get all chat rooms where user is a participant (including those with soft deleted users)
            var userChatRooms = await _context.ChatRooms
                .Include(cr => cr.User1)
                .Include(cr => cr.User2)
                .Include(cr => cr.MentorshipMatch)
                .Where(cr => (cr.User1Id == userId || cr.User2Id == userId) && cr.IsActive)
                .OrderByDescending(cr => cr.LastActivityAt ?? cr.CreatedAt)
                .ToListAsync();

            var chatList = new List<ChatListItemViewModel>();

            foreach (var chatRoom in userChatRooms)
            {
                // Get the partner (other user in the chat)
                var partner = chatRoom.User1Id == userId ? chatRoom.User2 : chatRoom.User1;

                // Get last message
                var lastMessage = await _context.ChatMessages
                    .Where(m => m.ChatRoomId == chatRoom.Id && !m.IsDeleted)
                    .OrderByDescending(m => m.SentAt)
                    .FirstOrDefaultAsync();

                // Get unread count (don't count unread messages if partner is soft deleted)
                var unreadCount = 0;
                if (!partner.IsDeleted)
                {
                    unreadCount = await _context.ChatMessages
                        .CountAsync(m => m.ChatRoomId == chatRoom.Id &&
                                        m.SenderId != userId &&
                                        !m.IsRead &&
                                        !m.IsDeleted);
                }

                string lastMessageText = "No messages yet";
                DateTime lastMessageTime = chatRoom.CreatedAt;

                if (lastMessage != null)
                {
                    lastMessageTime = lastMessage.SentAt;

                    // **CRITICAL FIX**: Check message type first before attempting decryption
                    if (lastMessage.MessageType == "file" || lastMessage.MessageType == "image" || lastMessage.MessageType == "video")
                    {
                        // For file/image/video messages, don't try to decrypt - just show "Sent an attachment"
                        lastMessageText = "Sent an attachment";
                    }
                    else if (lastMessage.MessageType == "system")
                    {
                        // System messages are not encrypted
                        lastMessageText = lastMessage.Message;
                    }
                    else
                    {
                        // Only decrypt text messages
                        try
                        {
                            var encryptionKey = _encryptionService.GenerateRoomKey(chatRoom.Id.ToString());
                            var decryptedMessage = _encryptionService.DecryptMessage(lastMessage.Message, encryptionKey);
                            lastMessageText = decryptedMessage;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to decrypt last message: {ex.Message}");
                            lastMessageText = "Message unavailable";
                        }
                    }
                }

                // Modify room name to indicate if user is unavailable
                var roomName = GetRoomName(chatRoom, partner);
                if (partner.IsDeleted)
                {
                    roomName += " (User unavailable)";
                }

                chatList.Add(new ChatListItemViewModel
                {
                    ChatRoomId = chatRoom.Id,
                    RoomName = roomName,
                    RoomType = chatRoom.RoomType,
                    Partner = partner,
                    LastMessage = lastMessageText,
                    LastMessageTime = lastMessageTime,
                    UnreadCount = unreadCount,
                    IsActive = chatRoomId == chatRoom.Id,
                    MentorshipMatch = chatRoom.MentorshipMatch
                });
            }

            // If no specific chat room is selected and no target user is specified, select the first one
            if (!chatRoomId.HasValue && string.IsNullOrEmpty(targetUserId) && chatList.Any())
            {
                chatRoomId = chatList.First().ChatRoomId;
            }

            // Get messages for selected chat room
            List<ChatMessageViewModel> messages = new();
            ChatViewModel selectedChat = null;

            if (chatRoomId.HasValue)
            {
                var chatRoom = userChatRooms.FirstOrDefault(cr => cr.Id == chatRoomId.Value);
                if (chatRoom != null)
                {
                    var partner = chatRoom.User1Id == userId ? chatRoom.User2 : chatRoom.User1;

                    // Get and decrypt messages
                    var encryptionKey = _encryptionService.GenerateRoomKey(chatRoomId.Value.ToString());
                    messages = await GetDecryptedMessages(chatRoomId.Value, userId, encryptionKey);

                    var roomName = GetRoomName(chatRoom, partner);
                    if (partner.IsDeleted)
                    {
                        roomName += " (User unavailable)";
                    }

                    selectedChat = new ChatViewModel
                    {
                        ChatRoomId = chatRoom.Id,
                        RoomName = roomName,
                        RoomType = chatRoom.RoomType,
                        Partner = partner,
                        CurrentUserId = userId,
                        Messages = messages,
                        MentorshipMatch = chatRoom.MentorshipMatch
                    };

                    // Mark messages as read (only if partner is not soft deleted)
                    if (!partner.IsDeleted)
                    {
                        await MarkMessagesAsRead(chatRoomId.Value, userId);
                    }
                }
            }
            else if (!string.IsNullOrEmpty(targetUserId))
            {
                // Handle case where we want to start a new chat with a target user
                // Only allow if target user is not soft deleted
                var targetUser = await _context.UserAccounts
                    .FirstOrDefaultAsync(u => u.Id == targetUserId && !u.IsDeleted);

                if (targetUser != null)
                {
                    selectedChat = new ChatViewModel
                    {
                        ChatRoomId = Guid.Empty, // Indicates no chat room exists yet
                        RoomName = $"Chat with {targetUser.FirstName} {targetUser.LastName}",
                        RoomType = "General",
                        Partner = targetUser,
                        CurrentUserId = userId,
                        Messages = new List<ChatMessageViewModel>(),
                        TargetUserId = targetUserId // Store target user ID for creating chat room later
                    };
                }
            }

            ViewBag.ChatList = chatList;
            ViewBag.SelectedChat = selectedChat;
            ViewBag.CurrentUserId = userId;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetMessages(Guid chatRoomId, int page = 1, int pageSize = 50)
        {
            var userId = GetCurrentUserId();

            // Verify user has access to this chat room (allow even if partner is soft deleted)
            var chatRoom = await _context.ChatRooms
                .Include(cr => cr.User1)
                .Include(cr => cr.User2)
                .FirstOrDefaultAsync(cr => cr.Id == chatRoomId &&
                                         (cr.User1Id == userId || cr.User2Id == userId) &&
                                         cr.IsActive);

            if (chatRoom == null)
            {
                return Json(new { success = false, message = "Access denied or chat room not found" });
            }

            var encryptionKey = _encryptionService.GenerateRoomKey(chatRoomId.ToString());
            var messages = await GetDecryptedMessagesPage(chatRoomId, userId, encryptionKey, page, pageSize);

            // Check if chat is disabled due to soft deleted partner
            var partner = chatRoom.User1Id == userId ? chatRoom.User2 : chatRoom.User1;
            var isChatDisabled = partner.IsDeleted;

            return Json(new { success = true, messages = messages, isChatDisabled = isChatDisabled });
        }

        [HttpGet]
        public async Task<IActionResult> StartChat(string targetUserId)
        {
            var currentUserId = GetCurrentUserId();

            if (currentUserId == targetUserId)
            {
                return RedirectToAction("Index");
            }

            // Check if target user exists and is not soft deleted
            var targetUser = await _context.UserAccounts
                .FirstOrDefaultAsync(u => u.Id == targetUserId);

            if (targetUser == null)
            {
                return NotFound("User not found");
            }

            if (targetUser.IsDeleted)
            {
                TempData["ErrorMessage"] = "Cannot start a chat with this user as their account is no longer available.";
                return RedirectToAction("Index");
            }

            // Check if a chat room already exists between these users
            var existingChatRoom = await _context.ChatRooms
                .Include(cr => cr.User1)
                .Include(cr => cr.User2)
                .FirstOrDefaultAsync(cr =>
                    ((cr.User1Id == currentUserId && cr.User2Id == targetUserId) ||
                     (cr.User1Id == targetUserId && cr.User2Id == currentUserId)) &&
                    cr.RoomType == "General");

            if (existingChatRoom != null)
            {
                // Chat room exists, redirect to it
                return RedirectToAction("Index", new { chatRoomId = existingChatRoom.Id });
            }

            // Instead of creating a chat room, redirect to a temporary chat view
            // The chat room will be created when the first message is sent
            return RedirectToAction("Index", new { targetUserId = targetUserId });
        }

        [HttpPost]
        public async Task<IActionResult> UploadFile(Guid chatRoomId, IFormFile file)
        {
            try
            {
                var userId = GetCurrentUserId();

                // Verify user has access to this chat room and partner is not soft deleted
                var chatRoom = await _context.ChatRooms
                    .Include(cr => cr.User1)
                    .Include(cr => cr.User2)
                    .FirstOrDefaultAsync(cr => cr.Id == chatRoomId &&
                                             (cr.User1Id == userId || cr.User2Id == userId) &&
                                             cr.IsActive);

                if (chatRoom == null)
                {
                    return Json(new { success = false, message = "Access denied or chat room not found" });
                }

                // Check if partner is soft deleted
                var partner = chatRoom.User1Id == userId ? chatRoom.User2 : chatRoom.User1;
                if (partner.IsDeleted)
                {
                    return Json(new { success = false, message = "Cannot send files to this user as their account is no longer available" });
                }

                if (file == null || file.Length == 0)
                {
                    return Json(new { success = false, message = "No file provided" });
                }

                if (file.Length > MaxFileSize)
                {
                    return Json(new { success = false, message = "File size exceeds maximum limit of 10MB" });
                }

                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedFileTypes.Contains(fileExtension))
                {
                    return Json(new { success = false, message = "File type not allowed" });
                }

                try
                {
                    // Upload file to Google Cloud Storage
                    var fileUrl = await _googleCloudStorageService.UploadFileAsync(file, "chat");

                    return Json(new
                    {
                        success = true,
                        fileName = file.FileName,
                        fileUrl = fileUrl,
                        fileSize = file.Length,
                        fileType = file.ContentType
                    });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = $"Upload failed: {ex.Message}" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Upload failed: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetChatList()
        {
            var userId = GetCurrentUserId();

            // Get all chat rooms where user is a participant (including those with soft deleted users)
            var userChatRooms = await _context.ChatRooms
                .Include(cr => cr.User1)
                .Include(cr => cr.User2)
                .Include(cr => cr.MentorshipMatch)
                .Where(cr => (cr.User1Id == userId || cr.User2Id == userId) && cr.IsActive)
                .OrderByDescending(cr => cr.LastActivityAt ?? cr.CreatedAt)
                .ToListAsync();

            var chatList = new List<object>();

            foreach (var chatRoom in userChatRooms)
            {
                // Get the partner (other user in the chat)
                var partner = chatRoom.User1Id == userId ? chatRoom.User2 : chatRoom.User1;

                // Get last message
                var lastMessage = await _context.ChatMessages
                    .Where(m => m.ChatRoomId == chatRoom.Id && !m.IsDeleted)
                    .OrderByDescending(m => m.SentAt)
                    .FirstOrDefaultAsync();

                // Get unread count (don't count unread messages if partner is soft deleted)
                var unreadCount = 0;
                if (!partner.IsDeleted)
                {
                    unreadCount = await _context.ChatMessages
                        .CountAsync(m => m.ChatRoomId == chatRoom.Id &&
                                        m.SenderId != userId &&
                                        !m.IsRead &&
                                        !m.IsDeleted);
                }

                string lastMessageText = "No messages yet";
                DateTime lastMessageTime = chatRoom.CreatedAt;

                if (lastMessage != null)
                {
                    lastMessageTime = lastMessage.SentAt;

                    // **CRITICAL FIX**: Check message type first before attempting decryption
                    if (lastMessage.MessageType == "file" || lastMessage.MessageType == "image" || lastMessage.MessageType == "video")
                    {
                        // For file/image/video messages, don't try to decrypt - just show "Sent an attachment"
                        lastMessageText = "Sent an attachment";
                    }
                    else if (lastMessage.MessageType == "system")
                    {
                        // System messages are not encrypted
                        lastMessageText = lastMessage.Message;
                    }
                    else
                    {
                        // Only decrypt text messages
                        try
                        {
                            var encryptionKey = _encryptionService.GenerateRoomKey(chatRoom.Id.ToString());
                            var decryptedMessage = _encryptionService.DecryptMessage(lastMessage.Message, encryptionKey);
                            lastMessageText = decryptedMessage;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to decrypt last message: {ex.Message}");
                            lastMessageText = "Message unavailable";
                        }
                    }
                }

                // Modify room name to indicate if user is unavailable
                var roomName = GetRoomName(chatRoom, partner);
                if (partner.IsDeleted)
                {
                    roomName += " (User unavailable)";
                }

                chatList.Add(new
                {
                    ChatRoomId = chatRoom.Id,
                    RoomName = roomName,
                    RoomType = chatRoom.RoomType,
                    Partner = new
                    {
                        Id = partner.Id,
                        FirstName = partner.FirstName,
                        LastName = partner.LastName,
                        Photo = partner.Photo,
                        IsDeleted = partner.IsDeleted
                    },
                    LastMessage = lastMessageText,
                    LastMessageTime = lastMessageTime,
                    UnreadCount = unreadCount,
                    MentorshipMatch = chatRoom.MentorshipMatch,
                    IsChatDisabled = partner.IsDeleted
                });
            }

            return Json(new { success = true, chatList = chatList });
        }

        [HttpGet("api/messages/unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            try
            {
                var userId = GetCurrentUserId();

                // Get total unread count across all chat rooms for the user
                // Only count messages from active users (not soft deleted)
                var totalUnreadCount = await _context.ChatMessages
                    .Include(m => m.ChatRoom)
                    .ThenInclude(cr => cr.User1)
                    .Include(m => m.ChatRoom)
                    .ThenInclude(cr => cr.User2)
                    .Include(m => m.Sender)
                    .Where(m => (m.ChatRoom.User1Id == userId || m.ChatRoom.User2Id == userId)
                               && m.SenderId != userId
                               && !m.IsRead
                               && !m.IsDeleted
                               && !m.Sender.IsDeleted) // Only count messages from non-deleted users
                    .CountAsync();

                return Ok(new { count = totalUnreadCount });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to get unread message count" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> VideoCall(Guid? chatRoomId = null)
        {
            var userId = GetCurrentUserId();

            // If chatRoomId is provided, try to find that specific chat room
            if (chatRoomId.HasValue)
            {
                var chatRoom = await _context.ChatRooms
                    .Include(cr => cr.User1)
                    .Include(cr => cr.User2)
                    .FirstOrDefaultAsync(cr => cr.Id == chatRoomId.Value &&
                                             (cr.User1Id == userId || cr.User2Id == userId) &&
                                             cr.IsActive);

                if (chatRoom != null)
                {
                    // Get the partner (other user in the chat)
                    var partner = chatRoom.User1Id == userId ? chatRoom.User2 : chatRoom.User1;

                    // Check if partner is soft deleted
                    if (partner.IsDeleted)
                    {
                        TempData["ErrorMessage"] = "Cannot start a video call with this user as their account is no longer available.";
                        return RedirectToAction("Index", new { chatRoomId = chatRoomId });
                    }

                    var viewModel = new ProjectVideoCallViewModel
                    {
                        ChatRoomId = chatRoom.Id.ToString(),
                        CurrentUserId = userId,
                        Partner = partner
                    };

                    return View(viewModel);
                }
            }

            // If no chat room found or no chatRoomId provided, try to find by targetUserId
            var targetUserId = Request.Query["targetUserId"].ToString();
            if (!string.IsNullOrEmpty(targetUserId))
            {
                // Check if target user exists and is not soft deleted
                var targetUser = await _context.UserAccounts
                    .FirstOrDefaultAsync(u => u.Id == targetUserId);

                if (targetUser == null || targetUser.IsDeleted)
                {
                    TempData["ErrorMessage"] = "Cannot start a video call with this user as their account is no longer available.";
                    return RedirectToAction("Index");
                }

                var existingChatRoom = await _context.ChatRooms
                    .Include(cr => cr.User1)
                    .Include(cr => cr.User2)
                    .FirstOrDefaultAsync(cr =>
                        ((cr.User1Id == userId && cr.User2Id == targetUserId) ||
                         (cr.User1Id == targetUserId && cr.User2Id == userId)) &&
                        cr.RoomType == "General" && cr.IsActive);

                if (existingChatRoom != null)
                {
                    var existingPartner = existingChatRoom.User1Id == userId ? existingChatRoom.User2 : existingChatRoom.User1;

                    // Double check if partner is soft deleted
                    if (existingPartner.IsDeleted)
                    {
                        TempData["ErrorMessage"] = "Cannot start a video call with this user as their account is no longer available.";
                        return RedirectToAction("Index", new { chatRoomId = existingChatRoom.Id });
                    }

                    var existingViewModel = new ProjectVideoCallViewModel
                    {
                        ChatRoomId = existingChatRoom.Id.ToString(),
                        CurrentUserId = userId,
                        Partner = existingPartner
                    };
                    return View(existingViewModel);
                }

                // If no existing chat room, create a temporary one for the video call
                var tempChatRoom = new ChatRoom
                {
                    Id = Guid.NewGuid(),
                    User1Id = userId,
                    User2Id = targetUserId,
                    RoomType = "General",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ChatRooms.Add(tempChatRoom);
                await _context.SaveChangesAsync();

                var tempViewModel = new ProjectVideoCallViewModel
                {
                    ChatRoomId = tempChatRoom.Id.ToString(),
                    CurrentUserId = userId,
                    Partner = targetUser
                };
                return View(tempViewModel);
            }

            return NotFound("Chat room not found or access denied");
        }

        // New method to check if chat is disabled
        [HttpGet]
        public async Task<IActionResult> CheckChatStatus(Guid chatRoomId)
        {
            var userId = GetCurrentUserId();

            var chatRoom = await _context.ChatRooms
                .Include(cr => cr.User1)
                .Include(cr => cr.User2)
                .FirstOrDefaultAsync(cr => cr.Id == chatRoomId &&
                                         (cr.User1Id == userId || cr.User2Id == userId) &&
                                         cr.IsActive);

            if (chatRoom == null)
            {
                return Json(new { success = false, message = "Chat room not found" });
            }

            var partner = chatRoom.User1Id == userId ? chatRoom.User2 : chatRoom.User1;
            var isChatDisabled = partner.IsDeleted;

            return Json(new
            {
                success = true,
                isChatDisabled = isChatDisabled,
                message = isChatDisabled ? "This user's account is no longer available" : ""
            });
        }

        private string GetRoomName(ChatRoom chatRoom, UserAccount partner)
        {
            switch (chatRoom.RoomType)
            {
                case "Mentorship":
                    return $"Mentorship with {partner.FirstName}";
                default:
                    return $"Chat with {partner.FirstName} {partner.LastName}";
            }
        }

        private string GetCurrentUserId()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedAccessException("User not authenticated");
            }
            return userId;
        }

        private async Task<List<ChatMessageViewModel>> GetDecryptedMessages(Guid chatRoomId, string userId, string encryptionKey)
        {
            var messages = await _context.ChatMessages
                .Where(cm => cm.ChatRoomId == chatRoomId && !cm.IsDeleted)
                .Include(cm => cm.Sender)
                .OrderBy(cm => cm.SentAt)
                .ToListAsync();

            var decryptedMessages = new List<ChatMessageViewModel>();

            foreach (var message in messages)
            {
                string decryptedContent;
                try
                {
                    if (message.MessageType == "system")
                    {
                        decryptedContent = message.Message; // System messages are not encrypted
                    }
                    else if (message.MessageType == "file" || message.MessageType == "image" || message.MessageType == "video")
                    {
                        // **CRITICAL FIX**: For file messages, don't decrypt - use filename directly
                        decryptedContent = message.Message; // This contains the filename
                    }
                    else
                    {
                        // Only decrypt text messages
                        decryptedContent = _encryptionService.DecryptMessage(message.Message, encryptionKey);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Decryption failed for message {message.Id}: {ex.Message}");
                    // Fallback to original message if decryption fails
                    decryptedContent = message.Message;
                }

                decryptedMessages.Add(new ChatMessageViewModel
                {
                    Id = message.Id,
                    SenderId = message.SenderId,
                    SenderName = message.MessageType == "system" ? "System" : $"{message.Sender.FirstName} {message.Sender.LastName}",
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

        private async Task<List<dynamic>> GetDecryptedMessagesPage(Guid chatRoomId, string userId, string encryptionKey, int page, int pageSize)
        {
            var messages = await _context.ChatMessages
                .Where(cm => cm.ChatRoomId == chatRoomId && !cm.IsDeleted)
                .Include(cm => cm.Sender)
                .OrderByDescending(cm => cm.SentAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var decryptedMessages = new List<dynamic>();

            foreach (var message in messages)
            {
                string decryptedContent;
                try
                {
                    if (message.MessageType == "system")
                    {
                        decryptedContent = message.Message; // System messages are not encrypted
                    }
                    else if (message.MessageType == "file" || message.MessageType == "image" || message.MessageType == "video")
                    {
                        // **CRITICAL FIX**: For file messages, don't decrypt - use filename directly
                        decryptedContent = message.Message; // This contains the filename
                    }
                    else
                    {
                        // Only decrypt text messages
                        decryptedContent = _encryptionService.DecryptMessage(message.Message, encryptionKey);
                    }
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
                    SenderName = message.MessageType == "system" ? "System" : $"{message.Sender.FirstName} {message.Sender.LastName}",
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

        private async Task MarkMessagesAsRead(Guid chatRoomId, string userId)
        {
            var unreadMessages = await _context.ChatMessages
                .Where(m => m.ChatRoomId == chatRoomId &&
                           m.SenderId != userId &&
                           !m.IsRead &&
                           !m.IsDeleted)
                .ToListAsync();

            foreach (var message in unreadMessages)
            {
                message.IsRead = true;
                message.ReadAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }
    }
}