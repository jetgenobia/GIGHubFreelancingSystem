using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Freelancing.Data;
using Freelancing.Models.Entities;
using Freelancing.Services;
using Microsoft.EntityFrameworkCore;

namespace Freelancing.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ApplicationDbContext _context;
        private readonly IMessageEncryptionService _encryptionService;
        private readonly ILogger<ChatHub> _logger;
        private static readonly Dictionary<string, string> UserConnections = new();
        private static readonly Dictionary<string, List<string>> RoomConnections = new();
        private static readonly object _lockObject = new object();

        public ChatHub(ApplicationDbContext context, IMessageEncryptionService encryptionService, ILogger<ChatHub> logger)
        {
            _context = context;
            _encryptionService = encryptionService;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                lock (_lockObject)
                {
                    // Always update to latest connection (replace old ones)
                    UserConnections[userId] = Context.ConnectionId;
                }

                // Ensure user is in their personal room for global notifications
                var userRoomName = $"user_{userId}";
                await Groups.AddToGroupAsync(Context.ConnectionId, userRoomName);

                lock (_lockObject)
                {
                    if (!RoomConnections.ContainsKey(userRoomName))
                    {
                        RoomConnections[userRoomName] = new List<string>();
                    }
                    // Remove old connection if exists, add new one
                    RoomConnections[userRoomName].RemoveAll(conn => conn == Context.ConnectionId);
                    RoomConnections[userRoomName].Add(Context.ConnectionId);
                }

                await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
                _logger.LogInformation("User {UserId} connected with connection {ConnectionId}", userId, Context.ConnectionId);
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                lock (_lockObject)
                {
                    // Only remove if this is the current connection for the user
                    if (UserConnections.TryGetValue(userId, out var currentConnectionId) &&
                        currentConnectionId == Context.ConnectionId)
                    {
                        UserConnections.Remove(userId);
                    }
                }
                _logger.LogInformation("User {UserId} disconnected", userId);
            }

            // Clean up room connections more thoroughly
            lock (_lockObject)
            {
                var roomsToUpdate = new List<string>();
                var roomsToRemove = new List<string>();

                foreach (var room in RoomConnections.ToList())
                {
                    if (room.Value.Contains(Context.ConnectionId))
                    {
                        room.Value.Remove(Context.ConnectionId);
                        roomsToUpdate.Add(room.Key);

                        if (room.Value.Count == 0)
                        {
                            roomsToRemove.Add(room.Key);
                        }
                    }
                }

                foreach (var room in roomsToRemove)
                {
                    RoomConnections.Remove(room);
                }

                if (roomsToUpdate.Count > 0)
                {
                    _logger.LogInformation("Cleaned up rooms for disconnected user {UserId}: {Rooms}",
                        userId, string.Join(", ", roomsToUpdate));
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        public static async Task UpdateNotificationCount(IHubContext<ChatHub> hubContext, string userId, int count)
        {
            string? connectionId = null;
            lock (_lockObject)
            {
                UserConnections.TryGetValue(userId, out connectionId);
            }

            if (!string.IsNullOrEmpty(connectionId))
            {
                await hubContext.Clients.Client(connectionId).SendAsync("UpdateNotificationCount", count);
            }
        }

        public async Task JoinChatRoom(string chatRoomId)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId)) return;

                // Verify user is part of this chat room
                var chatRoom = await _context.ChatRooms
                    .Include(cr => cr.User1)
                    .Include(cr => cr.User2)
                    .FirstOrDefaultAsync(cr => cr.Id.ToString() == chatRoomId &&
                                             (cr.User1Id == userId || cr.User2Id == userId) &&
                                             cr.IsActive);

                if (chatRoom == null) return;

                var roomName = $"chat_{chatRoomId}";
                await Groups.AddToGroupAsync(Context.ConnectionId, roomName);

                lock (_lockObject)
                {
                    if (!RoomConnections.ContainsKey(roomName))
                    {
                        RoomConnections[roomName] = new List<string>();
                    }
                    RoomConnections[roomName].Add(Context.ConnectionId);
                }

                await Clients.Caller.SendAsync("JoinedRoom", roomName);
                await Clients.OthersInGroup(roomName).SendAsync("UserJoined", Context.User.Identity?.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining chat room {ChatRoomId}", chatRoomId);
            }
        }

        public async Task JoinUserRoom(string userId)
        {
            try
            {
                var authenticatedUserId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(authenticatedUserId) || authenticatedUserId != userId)
                {
                    await Clients.Caller.SendAsync("Error", "Access denied");
                    return;
                }

                var roomName = $"user_{userId}";
                await Groups.AddToGroupAsync(Context.ConnectionId, roomName);

                lock (_lockObject)
                {
                    if (!RoomConnections.ContainsKey(roomName))
                    {
                        RoomConnections[roomName] = new List<string>();
                    }
                    RoomConnections[roomName].Add(Context.ConnectionId);
                }

                await Clients.Caller.SendAsync("JoinedUserRoom", roomName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining user room for user {UserId}", userId);
                await Clients.Caller.SendAsync("Error", "Failed to join user room");
            }
        }

        public async Task SendMessage(string chatRoomId, string message, string messageType = "text", string? targetUserId = null)
        {
            try
            {
                _logger.LogInformation("SendMessage called: chatRoomId={ChatRoomId}, messageType={MessageType}, targetUserId={TargetUserId}",
                    chatRoomId, messageType, targetUserId);

                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                var user = await _context.UserAccounts.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                {
                    await Clients.Caller.SendAsync("Error", "User not found");
                    return;
                }

                var fullName = $"{user.FirstName} {user.LastName}";
                ChatRoom? chatRoom = null;
                string actualChatRoomId = chatRoomId;

                // Handle new chat creation
                if (chatRoomId == "new" && !string.IsNullOrEmpty(targetUserId))
                {
                    chatRoom = await GetOrCreateChatRoom(userId, targetUserId);
                    if (chatRoom == null)
                    {
                        await Clients.Caller.SendAsync("Error", "Failed to create chat room");
                        return;
                    }
                    actualChatRoomId = chatRoom.Id.ToString();
                }
                else
                {
                    // Verify access to existing chat room
                    chatRoom = await _context.ChatRooms
                        .FirstOrDefaultAsync(cr => cr.Id.ToString() == chatRoomId &&
                                                 (cr.User1Id == userId || cr.User2Id == userId) &&
                                                 cr.IsActive);

                    if (chatRoom == null)
                    {
                        await Clients.Caller.SendAsync("Error", "Access denied or chat room not found");
                        return;
                    }
                }

                // Create and save message with UTC time
                var chatMessage = await CreateChatMessage(chatRoom, userId, message, messageType);

                // Update last activity with UTC time
                chatRoom.LastActivityAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Send message to room
                await SendMessageToRoom(actualChatRoomId, chatMessage, fullName, message, messageType);

                // Update notification count
                await UpdateOtherUserNotificationCount(chatRoom, userId);

                _logger.LogInformation("Message sent successfully to room {ChatRoomId}", actualChatRoomId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendMessage");
                await Clients.Caller.SendAsync("Error", $"Failed to send message: {ex.Message}");
            }
        }

        private async Task<ChatRoom?> GetOrCreateChatRoom(string userId, string targetUserId)
        {
            // Check if chat room exists
            var existingRoom = await _context.ChatRooms
                .FirstOrDefaultAsync(cr =>
                    ((cr.User1Id == userId && cr.User2Id == targetUserId) ||
                     (cr.User1Id == targetUserId && cr.User2Id == userId)) &&
                    cr.RoomType == "General" && cr.IsActive);

            if (existingRoom != null)
            {
                return existingRoom;
            }

            // Verify target user exists
            var targetUser = await _context.UserAccounts.FirstOrDefaultAsync(u => u.Id == targetUserId);
            if (targetUser == null)
            {
                await Clients.Caller.SendAsync("Error", "Target user not found");
                return null;
            }

            // Create new chat room with UTC time
            var newRoom = new ChatRoom
            {
                Id = Guid.NewGuid(),
                User1Id = userId,
                User2Id = targetUserId,
                RoomType = "General",
                CreatedAt = DateTime.UtcNow, // Use UTC
                LastActivityAt = DateTime.UtcNow, // Use UTC
                IsActive = true
            };

            _context.ChatRooms.Add(newRoom);
            await _context.SaveChangesAsync();

            // Join the new room
            await JoinNewChatRoom(newRoom.Id.ToString(), targetUserId);

            // Notify about new chat room
            await Clients.Caller.SendAsync("ChatRoomCreated", newRoom.Id.ToString());

            return newRoom;
        }

        private async Task JoinNewChatRoom(string chatRoomId, string targetUserId)
        {
            var newRoomName = $"chat_{chatRoomId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, newRoomName);

            // Add target user if online
            string? targetUserConnectionId = null;
            lock (_lockObject)
            {
                UserConnections.TryGetValue(targetUserId, out targetUserConnectionId);
            }

            if (!string.IsNullOrEmpty(targetUserConnectionId))
            {
                await Groups.AddToGroupAsync(targetUserConnectionId, newRoomName);
            }

            // Update room connections tracking
            lock (_lockObject)
            {
                if (!RoomConnections.ContainsKey(newRoomName))
                {
                    RoomConnections[newRoomName] = new List<string>();
                }
                RoomConnections[newRoomName].Add(Context.ConnectionId);
                if (!string.IsNullOrEmpty(targetUserConnectionId))
                {
                    RoomConnections[newRoomName].Add(targetUserConnectionId);
                }
            }
        }

        private async Task<ChatMessage> CreateChatMessage(ChatRoom chatRoom, string userId, string message, string messageType)
        {
            // Generate encryption key and encrypt message
            var encryptionKey = _encryptionService.GenerateRoomKey(chatRoom.Id.ToString());
            var encryptedMessage = _encryptionService.EncryptMessage(message, encryptionKey);

            var chatMessage = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ChatRoomId = chatRoom.Id,
                SenderId = userId,
                Message = encryptedMessage,
                MessageType = messageType,
                SentAt = DateTime.UtcNow, // Use UTC
                IsRead = false
            };

            _context.ChatMessages.Add(chatMessage);
            return chatMessage;
        }

        private async Task SendMessageToRoom(string chatRoomId, ChatMessage chatMessage, string senderName, string originalMessage, string messageType)
        {
            var roomName = $"chat_{chatRoomId}";

            // Ensure sender is in the room
            await EnsureSenderInRoom(roomName);

            var messageObject = new
            {
                Id = chatMessage.Id.ToString(),
                SenderId = chatMessage.SenderId,
                SenderName = senderName,
                Message = originalMessage, // Send decrypted message to clients
                MessageType = messageType,
                SentAt = chatMessage.SentAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"), // ISO format with UTC
                IsRead = false
            };

            await Clients.Group(roomName).SendAsync("ReceiveMessage", messageObject);
        }

        private async Task EnsureSenderInRoom(string roomName)
        {
            lock (_lockObject)
            {
                if (!RoomConnections.ContainsKey(roomName) || !RoomConnections[roomName].Contains(Context.ConnectionId))
                {
                    if (!RoomConnections.ContainsKey(roomName))
                    {
                        RoomConnections[roomName] = new List<string>();
                    }
                    if (!RoomConnections[roomName].Contains(Context.ConnectionId))
                    {
                        RoomConnections[roomName].Add(Context.ConnectionId);
                    }
                }
            }

            // Move the async operation outside the lock and await it properly
            await Groups.AddToGroupAsync(Context.ConnectionId, roomName);
        }

        private async Task UpdateOtherUserNotificationCount(ChatRoom chatRoom, string currentUserId)
        {
            var otherUserId = chatRoom.User1Id == currentUserId ? chatRoom.User2Id : chatRoom.User1Id;

            var totalUnreadCount = await _context.ChatMessages
                .Include(m => m.ChatRoom)
                .Where(m => (m.ChatRoom.User1Id == otherUserId || m.ChatRoom.User2Id == otherUserId)
                            && m.SenderId != otherUserId
                            && !m.IsRead
                            && !m.IsDeleted)
                .CountAsync();

            await UpdateNotificationCount(
                Context.GetHttpContext().RequestServices.GetRequiredService<IHubContext<ChatHub>>(),
                otherUserId,
                totalUnreadCount
            );
        }

        public async Task MarkAsRead(string chatRoomId)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId)) return;

                var chatRoom = await _context.ChatRooms
                    .FirstOrDefaultAsync(cr => cr.Id.ToString() == chatRoomId &&
                                             (cr.User1Id == userId || cr.User2Id == userId) &&
                                             cr.IsActive);

                if (chatRoom == null) return;

                var unreadMessages = await _context.ChatMessages
                    .Where(m => m.ChatRoomId == chatRoom.Id &&
                               m.SenderId != userId &&
                               !m.IsRead &&
                               !m.IsDeleted)
                    .ToListAsync();

                foreach (var message in unreadMessages)
                {
                    message.IsRead = true;
                    message.ReadAt = DateTime.UtcNow; // Use UTC
                }

                await _context.SaveChangesAsync();

                var roomName = $"chat_{chatRoomId}";
                await Clients.OthersInGroup(roomName).SendAsync("MessagesRead", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MarkAsRead for room {ChatRoomId}", chatRoomId);
            }
        }

        public async Task GetTotalUnreadCount()
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId)) return;

                var totalUnreadCount = await _context.ChatMessages
                    .Include(m => m.ChatRoom)
                    .Where(m => (m.ChatRoom.User1Id == userId || m.ChatRoom.User2Id == userId)
                               && m.SenderId != userId
                               && !m.IsRead
                               && !m.IsDeleted)
                    .CountAsync();

                await Clients.Caller.SendAsync("TotalUnreadCount", totalUnreadCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting total unread count");
                await Clients.Caller.SendAsync("Error", "Failed to get unread count");
            }
        }

        // Keep your existing video call and file methods but update DateTime usage to UTC
        // I'll show one example with SendFile:

        public async Task SendFile(string chatRoomId, string fileName, string fileUrl, long fileSize, string fileType)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                var user = await _context.UserAccounts.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                {
                    await Clients.Caller.SendAsync("Error", "User not found");
                    return;
                }

                var chatRoom = await _context.ChatRooms
                    .FirstOrDefaultAsync(cr => cr.Id.ToString() == chatRoomId &&
                                             (cr.User1Id == userId || cr.User2Id == userId) &&
                                             cr.IsActive);

                if (chatRoom == null)
                {
                    await Clients.Caller.SendAsync("Error", "Access denied or chat room not found");
                    return;
                }

                // Determine message type
                string messageType = DetermineFileMessageType(fileType, fileName);

                // Create file message with UTC time
                var chatMessage = new ChatMessage
                {
                    Id = Guid.NewGuid(),
                    ChatRoomId = chatRoom.Id,
                    SenderId = userId,
                    Message = fileName, // Store filename directly or encrypt if needed
                    MessageType = messageType,
                    FileUrl = fileUrl,
                    FileType = fileType,
                    FileSize = fileSize,
                    SentAt = DateTime.UtcNow, // Use UTC
                    IsRead = false
                };

                _context.ChatMessages.Add(chatMessage);
                chatRoom.LastActivityAt = DateTime.UtcNow; // Use UTC
                await _context.SaveChangesAsync();

                // Send file message
                var roomName = $"chat_{chatRoomId}";
                var fileMessageObject = new
                {
                    Id = chatMessage.Id.ToString(),
                    SenderId = userId,
                    SenderName = $"{user.FirstName} {user.LastName}",
                    FileName = fileName,
                    FileUrl = fileUrl,
                    FileType = fileType,
                    FileSize = fileSize,
                    MessageType = messageType,
                    SentAt = chatMessage.SentAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"), // ISO format with UTC
                    IsRead = false
                };

                await Clients.Group(roomName).SendAsync("ReceiveFile", fileMessageObject);
                await UpdateOtherUserNotificationCount(chatRoom, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendFile");
                await Clients.Caller.SendAsync("Error", $"Failed to send file: {ex.Message}");
            }
        }

        private static string DetermineFileMessageType(string? fileType, string fileName)
        {
            if (fileType != null)
            {
                if (fileType.StartsWith("image/")) return "image";
                if (fileType.StartsWith("video/")) return "video";
            }

            var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
            if (new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" }.Contains(fileExtension))
                return "image";
            if (new[] { ".mp4", ".mov", ".avi", ".wmv", ".flv", ".webm" }.Contains(fileExtension))
                return "video";

            return "file";
        }

        // Add these methods to your ChatHub class, after the existing methods

        #region Video Call Methods

        public async Task StartVideoCall(string chatRoomId)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                // Get user info for caller name and photo
                var user = await _context.UserAccounts.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                {
                    await Clients.Caller.SendAsync("Error", "User not found");
                    return;
                }
                var fullName = $"{user.FirstName} {user.LastName}";

                // Check if this is a temporary chat room ID (for new chats)
                if (chatRoomId.StartsWith("temp_"))
                {
                    await Clients.Caller.SendAsync("CallRequested", new
                    {
                        ChatRoomId = chatRoomId,
                        CallerId = userId,
                        CallerName = fullName ?? "Unknown User",
                        CallerPhoto = !string.IsNullOrEmpty(user.Photo) ? user.Photo : "https://ik.imagekit.io/6txj3mofs/GIGHub%20(11).png?updatedAt=1750552804497",
                        IsTemporary = true
                    });
                    return;
                }

                // Verify user is part of this chat room
                var chatRoom = await _context.ChatRooms
                    .Include(cr => cr.User1)
                    .Include(cr => cr.User2)
                    .FirstOrDefaultAsync(cr => cr.Id.ToString() == chatRoomId &&
                                             (cr.User1Id == userId || cr.User2Id == userId) &&
                                             cr.IsActive);

                if (chatRoom == null)
                {
                    await Clients.Caller.SendAsync("Error", "Access denied or chat room not found");
                    return;
                }

                var roomName = $"chat_{chatRoomId}";
                var partnerId = chatRoom.User1Id == userId ? chatRoom.User2Id : chatRoom.User1Id;
                var partnerRoomName = $"user_{partnerId}";

                // Clean up and ensure proper room membership
                await CleanUpRoomConnections(roomName);
                await EnsureSenderInRoom(roomName);
                await EnsureUserInPersonalRoom(partnerId);

                // Create the call data object
                var callData = new
                {
                    CallerId = userId,
                    CallerName = fullName ?? "Unknown User",
                    CallerPhoto = !string.IsNullOrEmpty(user.Photo) ? user.Photo : "https://ik.imagekit.io/6txj3mofs/GIGHub%20(11).png?updatedAt=1750552804497",
                    ChatRoomId = chatRoomId
                };

                // Send caller notification first
                await Clients.Caller.SendAsync("CallRequested", callData);

                // **CRITICAL FIX**: Send notification ONLY to partner's personal room to avoid duplicates
                // Do NOT send to chat room to prevent duplicate notifications
                await Clients.Group(partnerRoomName).SendAsync("IncomingVideoCall", callData);

                await Clients.Caller.SendAsync("VideoCallInitiated", chatRoomId);

                _logger.LogInformation("Video call started in room {ChatRoomId} by user {UserId}, notified partner {PartnerId} via personal room only",
                    chatRoomId, userId, partnerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting video call in room {ChatRoomId}", chatRoomId);
                await Clients.Caller.SendAsync("Error", "Failed to start video call");
            }
        }

        private async Task CleanUpRoomConnections(string roomName)
        {
            lock (_lockObject)
            {
                if (RoomConnections.ContainsKey(roomName))
                {
                    // Remove duplicate or stale connections
                    var currentConnections = RoomConnections[roomName].ToList();
                    var uniqueConnections = currentConnections.Distinct().ToList();

                    if (uniqueConnections.Count != currentConnections.Count)
                    {
                        RoomConnections[roomName] = uniqueConnections;
                        _logger.LogInformation("Cleaned up duplicate connections in room {RoomName}: {Before} → {After}",
                            roomName, currentConnections.Count, uniqueConnections.Count);
                    }
                }
            }
        }

        private async Task EnsureUserInPersonalRoom(string userId)
        {
            string? connectionId = null;
            lock (_lockObject)
            {
                UserConnections.TryGetValue(userId, out connectionId);
            }

            if (!string.IsNullOrEmpty(connectionId))
            {
                var userRoomName = $"user_{userId}";
                await Groups.AddToGroupAsync(connectionId, userRoomName);

                lock (_lockObject)
                {
                    if (!RoomConnections.ContainsKey(userRoomName))
                    {
                        RoomConnections[userRoomName] = new List<string>();
                    }
                    if (!RoomConnections[userRoomName].Contains(connectionId))
                    {
                        RoomConnections[userRoomName].Add(connectionId);
                    }
                }
            }
        }

        public async Task SendOffer(string chatRoomId, string offer)
        {
            try
            {
                var roomName = $"chat_{chatRoomId}";
                await Clients.OthersInGroup(roomName).SendAsync("ReceiveOffer", offer);
                _logger.LogInformation("WebRTC offer sent to room {ChatRoomId}", chatRoomId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending WebRTC offer to room {ChatRoomId}", chatRoomId);
            }
        }

        public async Task SendAnswer(string chatRoomId, string answer)
        {
            try
            {
                var roomName = $"chat_{chatRoomId}";
                await Clients.OthersInGroup(roomName).SendAsync("ReceiveAnswer", answer);
                _logger.LogInformation("WebRTC answer sent to room {ChatRoomId}", chatRoomId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending WebRTC answer to room {ChatRoomId}", chatRoomId);
            }
        }

        public async Task SendIceCandidate(string chatRoomId, string candidate)
        {
            try
            {
                var roomName = $"chat_{chatRoomId}";
                await Clients.OthersInGroup(roomName).SendAsync("ReceiveIceCandidate", candidate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending ICE candidate to room {ChatRoomId}", chatRoomId);
            }
        }

        public async Task EndVideoCall(string chatRoomId)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var roomName = $"chat_{chatRoomId}";

                // **CRITICAL FIX**: Also notify caller's personal room to hide waiting notification
                var callerRoomName = $"user_{userId}";

                await Clients.Group(roomName).SendAsync("VideoCallEnded", new
                {
                    chatRoomId = chatRoomId,      // camelCase
                    endedByUserId = userId        // camelCase
                });

                // Also notify caller's personal room to hide any waiting notifications
                await Clients.Group(callerRoomName).SendAsync("VideoCallEnded", new
                {
                    chatRoomId = chatRoomId,      // camelCase
                    endedByUserId = userId        // camelCase
                });

                _logger.LogInformation("Video call ended in room {ChatRoomId} by user {UserId}", chatRoomId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ending video call in room {ChatRoomId}", chatRoomId);
            }
        }

        public async Task AcceptVideoCall(string chatRoomId, string callerId)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                // Check if this is a temporary chat room ID
                if (chatRoomId.StartsWith("temp_"))
                {
                    // For temporary chat rooms, we need to create a real chat room
                    var caller = await _context.UserAccounts.FirstOrDefaultAsync(u => u.Id == callerId);
                    if (caller == null)
                    {
                        await Clients.Caller.SendAsync("Error", "Caller not found");
                        return;
                    }

                    // Create a new chat room for this video call
                    var newChatRoom = new ChatRoom
                    {
                        Id = Guid.NewGuid(),
                        User1Id = callerId,
                        User2Id = userId,
                        RoomType = "General",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        LastActivityAt = DateTime.UtcNow
                    };

                    _context.ChatRooms.Add(newChatRoom);
                    await _context.SaveChangesAsync();

                    // **CRITICAL FIX**: Use camelCase property names for consistency
                    // Notify both caller and accepter with the new chat room ID
                    string? callerConnectionId = null;
                    lock (_lockObject)
                    {
                        UserConnections.TryGetValue(callerId, out callerConnectionId);
                    }

                    if (!string.IsNullOrEmpty(callerConnectionId))
                    {
                        await Clients.Client(callerConnectionId).SendAsync("CallAccepted", new
                        {
                            chatRoomId = newChatRoom.Id.ToString(),  // camelCase
                            accepterId = userId,                     // camelCase
                            isTemporary = false                      // camelCase
                        });
                    }

                    await Clients.Caller.SendAsync("CallAccepted", new
                    {
                        chatRoomId = newChatRoom.Id.ToString(),  // camelCase
                        accepterId = userId,                     // camelCase
                        isTemporary = false                      // camelCase
                    });

                    _logger.LogInformation("Temporary video call accepted and chat room {ChatRoomId} created between {CallerId} and {AccepterId}",
                        newChatRoom.Id, callerId, userId);
                    return;
                }

                // Verify access to this chat room
                var chatRoom = await _context.ChatRooms
                    .FirstOrDefaultAsync(cr => cr.Id.ToString() == chatRoomId &&
                                             (cr.User1Id == userId || cr.User2Id == userId) &&
                                             cr.IsActive);

                if (chatRoom == null)
                {
                    await Clients.Caller.SendAsync("Error", "Access denied or chat room not found");
                    return;
                }

                // **CRITICAL FIX**: Use camelCase property names for consistency
                // Notify the caller that their call was accepted
                string? callerConnectionId2 = null;
                lock (_lockObject)
                {
                    UserConnections.TryGetValue(callerId, out callerConnectionId2);
                }

                if (!string.IsNullOrEmpty(callerConnectionId2))
                {
                    await Clients.Client(callerConnectionId2).SendAsync("CallAccepted", new
                    {
                        chatRoomId = chatRoomId,  // camelCase
                        accepterId = userId       // camelCase
                    });
                }

                // Also notify the accepter to open their video call window
                await Clients.Caller.SendAsync("CallAccepted", new
                {
                    chatRoomId = chatRoomId,  // camelCase
                    accepterId = userId       // camelCase
                });

                _logger.LogInformation("Video call accepted in room {ChatRoomId} by user {UserId}", chatRoomId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting video call in room {ChatRoomId}", chatRoomId);
                await Clients.Caller.SendAsync("Error", "Failed to accept video call");
            }
        }

        public async Task DeclineVideoCall(string chatRoomId, string callerId)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                // Check if this is a temporary chat room ID
                if (chatRoomId.StartsWith("temp_"))
                {
                    // For temporary chat rooms, just notify the caller that their call was declined
                    string? callerConnectionId = null;
                    lock (_lockObject)
                    {
                        UserConnections.TryGetValue(callerId, out callerConnectionId);
                    }

                    if (!string.IsNullOrEmpty(callerConnectionId))
                    {
                        await Clients.Client(callerConnectionId).SendAsync("CallDeclined", new
                        {
                            chatRoomId = chatRoomId,  // Use camelCase
                            declinerId = userId,      // Use camelCase
                            isTemporary = true        // Use camelCase
                        });
                    }

                    _logger.LogInformation("Temporary video call declined in room {ChatRoomId} by user {UserId}", chatRoomId, userId);
                    return;
                }

                // Verify access to this chat room
                var chatRoom = await _context.ChatRooms
                    .FirstOrDefaultAsync(cr => cr.Id.ToString() == chatRoomId &&
                                             (cr.User1Id == userId || cr.User2Id == userId) &&
                                             cr.IsActive);

                if (chatRoom == null)
                {
                    await Clients.Caller.SendAsync("Error", "Access denied or chat room not found");
                    return;
                }

                // **CRITICAL FIX**: Notify the caller that their call was declined
                string? callerConnectionId2 = null;
                lock (_lockObject)
                {
                    UserConnections.TryGetValue(callerId, out callerConnectionId2);
                }

                if (!string.IsNullOrEmpty(callerConnectionId2))
                {
                    await Clients.Client(callerConnectionId2).SendAsync("CallDeclined", new
                    {
                        chatRoomId = chatRoomId,  // Use camelCase to match JavaScript expectations
                        declinerId = userId       // Use camelCase to match JavaScript expectations
                    });
                }

                _logger.LogInformation("Video call declined in room {ChatRoomId} by user {UserId}", chatRoomId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error declining video call in room {ChatRoomId}", chatRoomId);
                await Clients.Caller.SendAsync("Error", "Failed to decline video call");
            }
        }

        public async Task RejectVideoCall(string chatRoomId, string callerId)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                // Check if this is a temporary chat room ID
                if (chatRoomId.StartsWith("temp_"))
                {
                    // For temporary chat rooms, just notify the caller that their call was declined
                    string? callerConnectionId = null;
                    lock (_lockObject)
                    {
                        UserConnections.TryGetValue(callerId, out callerConnectionId);
                    }

                    if (!string.IsNullOrEmpty(callerConnectionId))
                    {
                        await Clients.Client(callerConnectionId).SendAsync("CallDeclined", new
                        {
                            ChatRoomId = chatRoomId,
                            DeclinerId = userId,
                            IsTemporary = true
                        });
                    }

                    _logger.LogInformation("Temporary video call declined in room {ChatRoomId} by user {UserId}", chatRoomId, userId);
                    return;
                }

                // Verify access to this chat room
                var chatRoom = await _context.ChatRooms
                    .FirstOrDefaultAsync(cr => cr.Id.ToString() == chatRoomId &&
                                             (cr.User1Id == userId || cr.User2Id == userId) &&
                                             cr.IsActive);

                if (chatRoom == null)
                {
                    await Clients.Caller.SendAsync("Error", "Access denied or chat room not found");
                    return;
                }

                var roomName = $"chat_{chatRoomId}";

                // Notify the caller that their call was declined
                string? callerConnectionId2 = null;
                lock (_lockObject)
                {
                    UserConnections.TryGetValue(callerId, out callerConnectionId2);
                }

                if (!string.IsNullOrEmpty(callerConnectionId2))
                {
                    await Clients.Client(callerConnectionId2).SendAsync("CallDeclined", new
                    {
                        ChatRoomId = chatRoomId,
                        DeclinerId = userId
                    });
                }

                await Clients.Group(roomName).SendAsync("VideoCallRejected", new
                {
                    ChatRoomId = chatRoomId,
                    RejectedByUserId = userId
                });

                _logger.LogInformation("Video call rejected in room {ChatRoomId} by user {UserId}", chatRoomId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting video call in room {ChatRoomId}", chatRoomId);
                await Clients.Caller.SendAsync("Error", "Failed to reject video call");
            }
        }

        #endregion
    }
}