﻿using Microsoft.AspNetCore.Mvc;
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
        private const int MaxFileSize = 10 * 1024 * 1024; // 10MB
        private readonly string[] AllowedFileTypes = { ".pdf", ".doc", ".docx", ".txt", ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".mp4", ".mov", ".avi", ".wmv", ".flv", ".webm" };

        public ChatController(ApplicationDbContext context, IWebHostEnvironment environment, IMessageEncryptionService encryptionService)
        {
            _context = context;
            _environment = environment;
            _encryptionService = encryptionService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(Guid? chatRoomId = null, Guid? targetUserId = null)
        {
            var userId = GetCurrentUserId();

            // Get all chat rooms where user is a participant
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

                // Get unread count
                var unreadCount = await _context.ChatMessages
                    .CountAsync(m => m.ChatRoomId == chatRoom.Id && 
                                    m.SenderId != userId && 
                                    !m.IsRead && 
                                    !m.IsDeleted);

                string lastMessageText = "No messages yet";
                DateTime lastMessageTime = chatRoom.CreatedAt;

                if (lastMessage != null)
                {
                    try
                    {
                        var encryptionKey = _encryptionService.GenerateRoomKey(chatRoom.Id.ToString());
                        var decryptedMessage = _encryptionService.DecryptMessage(lastMessage.Message, encryptionKey);
                        lastMessageTime = lastMessage.SentAt;
                        
                        // Check if it's a file, image, or video message
                        if (lastMessage.MessageType == "file" || lastMessage.MessageType == "image" || lastMessage.MessageType == "video")
                        {
                            lastMessageText = "Sent an attachment";
                        }
                        else
                        {
                            lastMessageText = decryptedMessage;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to decrypt last message: {ex.Message}");
                        lastMessageText = "Message unavailable";
                    }
                }

                chatList.Add(new ChatListItemViewModel
                {
                    ChatRoomId = chatRoom.Id,
                    RoomName = GetRoomName(chatRoom, partner),
                    RoomType = chatRoom.RoomType,
                    Partner = partner,
                    LastMessage = lastMessageText,
                    LastMessageTime = lastMessageTime,
                    UnreadCount = unreadCount,
                    IsActive = chatRoomId == chatRoom.Id,
                    MentorshipMatch = chatRoom.MentorshipMatch
                });
            }

            // If no specific chat room is selected, select the first one
            if (!chatRoomId.HasValue && chatList.Any())
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

                    selectedChat = new ChatViewModel
                    {
                        ChatRoomId = chatRoom.Id,
                        RoomName = GetRoomName(chatRoom, partner),
                        RoomType = chatRoom.RoomType,
                        Partner = partner,
                        CurrentUserId = userId,
                        Messages = messages,
                        MentorshipMatch = chatRoom.MentorshipMatch
                    };

                    // Mark messages as read
                    await MarkMessagesAsRead(chatRoomId.Value, userId);
                }
            }
            else if (targetUserId.HasValue)
            {
                // Handle case where we want to start a new chat with a target user
                var targetUser = await _context.UserAccounts
                    .FirstOrDefaultAsync(u => u.Id == targetUserId.Value);

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
                        TargetUserId = targetUserId.Value // Store target user ID for creating chat room later
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

            // Verify user has access to this chat room
            var chatRoom = await _context.ChatRooms
                .FirstOrDefaultAsync(cr => cr.Id == chatRoomId &&
                                         (cr.User1Id == userId || cr.User2Id == userId) &&
                                         cr.IsActive);

            if (chatRoom == null)
            {
                return Json(new { success = false, message = "Access denied or chat room not found" });
            }

            var encryptionKey = _encryptionService.GenerateRoomKey(chatRoomId.ToString());
            var messages = await GetDecryptedMessagesPage(chatRoomId, userId, encryptionKey, page, pageSize);

            return Json(new { success = true, messages = messages });
        }

        [HttpGet]
        public async Task<IActionResult> StartChat(Guid targetUserId)
        {
            var currentUserId = GetCurrentUserId();
            
            if (currentUserId == targetUserId)
            {
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

            // Get target user details
            var targetUser = await _context.UserAccounts
                .FirstOrDefaultAsync(u => u.Id == targetUserId);

            if (targetUser == null)
            {
                return NotFound("User not found");
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

                // Verify user has access to this chat room
                var chatRoom = await _context.ChatRooms
                    .FirstOrDefaultAsync(cr => cr.Id == chatRoomId &&
                                             (cr.User1Id == userId || cr.User2Id == userId) &&
                                             cr.IsActive);

                if (chatRoom == null)
                {
                    return Json(new { success = false, message = "Access denied or chat room not found" });
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

                // Create uploads directory if it doesn't exist
                var uploadsDir = Path.Combine(_environment.WebRootPath, "uploads", "chat");
                if (!Directory.Exists(uploadsDir))
                {
                    Directory.CreateDirectory(uploadsDir);
                }

                // Generate unique filename
                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                var filePath = Path.Combine(uploadsDir, fileName);

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var fileUrl = $"/uploads/chat/{fileName}";

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

        private Guid GetCurrentUserId()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid userId))
            {
                throw new UnauthorizedAccessException("User not authenticated");
            }
            return userId;
        }

        private async Task<List<ChatMessageViewModel>> GetDecryptedMessages(Guid chatRoomId, Guid userId, string encryptionKey)
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
                    else
                    {
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

        private async Task<List<dynamic>> GetDecryptedMessagesPage(Guid chatRoomId, Guid userId, string encryptionKey, int page, int pageSize)
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
                    else
                    {
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

        private async Task MarkMessagesAsRead(Guid chatRoomId, Guid userId)
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
                message.ReadAt = DateTime.UtcNow.ToLocalTime();
            }

            await _context.SaveChangesAsync();
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
            if (!string.IsNullOrEmpty(targetUserId) && Guid.TryParse(targetUserId, out Guid targetUserGuid))
            {
                var existingChatRoom = await _context.ChatRooms
                    .Include(cr => cr.User1)
                    .Include(cr => cr.User2)
                    .FirstOrDefaultAsync(cr => 
                        ((cr.User1Id == userId && cr.User2Id == targetUserGuid) || 
                         (cr.User1Id == targetUserGuid && cr.User2Id == userId)) && 
                        cr.RoomType == "General" && cr.IsActive);

                if (existingChatRoom != null)
                {
                    var existingPartner = existingChatRoom.User1Id == userId ? existingChatRoom.User2 : existingChatRoom.User1;
                    var existingViewModel = new ProjectVideoCallViewModel
                    {
                        ChatRoomId = existingChatRoom.Id.ToString(),
                        CurrentUserId = userId,
                        Partner = existingPartner
                    };
                    return View(existingViewModel);
                }

                // If no existing chat room, create a temporary one for the video call
                var targetUser = await _context.UserAccounts.FirstOrDefaultAsync(u => u.Id == targetUserGuid);
                if (targetUser != null)
                {
                    var tempChatRoom = new ChatRoom
                    {
                        Id = Guid.NewGuid(),
                        User1Id = userId,
                        User2Id = targetUserGuid,
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
            }
            
            return NotFound("Chat room not found or access denied");
        }
    }
}
