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
        private static readonly Dictionary<string, string> UserConnections = new();
        private static readonly Dictionary<string, List<string>> RoomConnections = new();
        private static readonly object _lockObject = new object();

        public ChatHub(ApplicationDbContext context, IMessageEncryptionService encryptionService)
        {
            _context = context;
            _encryptionService = encryptionService;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                lock (_lockObject)
                {
                    UserConnections[userId] = Context.ConnectionId;
                }
                await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
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
                    UserConnections.Remove(userId);
                }
            }

            // Remove from all rooms
            lock (_lockObject)
            {
                var roomsToRemove = new List<string>();
                foreach (var room in RoomConnections)
                {
                    if (room.Value.Contains(Context.ConnectionId))
                    {
                        room.Value.Remove(Context.ConnectionId);
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
            }

            await base.OnDisconnectedAsync(exception);
        }

        // Add method to update notification count
        public static async Task UpdateNotificationCount(IHubContext<ChatHub> hubContext, string userId, int count)
        {
            string connectionId = null;
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

                // Verify user is part of this chat room
                var chatRoom = await _context.ChatRooms
                    .Include(cr => cr.User1)
                    .Include(cr => cr.User2)
                    .FirstOrDefaultAsync(cr => cr.Id.ToString() == chatRoomId &&
                                             (cr.User1Id == userId || cr.User2Id == userId) &&
                                             cr.IsActive);

                if (chatRoom == null)
                {
                    // Don't send error for missing chat rooms - they might be created later
                    return;
                }

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
                await Clients.OthersInGroup(roomName).SendAsync("UserJoined", Context.User.Identity.Name);
            }
            catch (Exception ex)
            {
                // Log the error but don't send it to the client to avoid spam
                Console.WriteLine($"Error joining chat room {chatRoomId}: {ex.Message}");
            }
        }

        public async Task JoinUserRoom(string userId)
        {
            try
            {
                // Verify the user ID matches the authenticated user
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
                await Clients.Caller.SendAsync("Error", "Failed to join user room");
            }
        }

        public async Task SendMessage(string chatRoomId, string message, string messageType = "text", string targetUserId = null)
        {
            try
            {
                Console.WriteLine($"SendMessage called with: chatRoomId={chatRoomId}, message={message}, messageType={messageType}, targetUserId={targetUserId}");
                
                var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                var userId = userIdClaim;
                var user = await _context.UserAccounts.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                {
                    await Clients.Caller.SendAsync("Error", "User not found");
                    return;
                }

                var fullName = $"{user.FirstName} {user.LastName}";

                ChatRoom chatRoom = null;
                string actualChatRoomId = chatRoomId;
                
                Console.WriteLine($"Initial chatRoomId: {chatRoomId}, actualChatRoomId: {actualChatRoomId}");

                // Check if this is a new chat (chatRoomId is "new" and targetUserId is provided)
                if (chatRoomId == "new" && !string.IsNullOrEmpty(targetUserId))
                {
                    var targetUserIdString = targetUserId;
                    
                    // Check if a chat room already exists between these users
                    chatRoom = await _context.ChatRooms
                        .FirstOrDefaultAsync(cr => 
                            ((cr.User1Id == userId && cr.User2Id == targetUserIdString) || 
                             (cr.User1Id == targetUserIdString && cr.User2Id == userId)) && 
                            cr.RoomType == "General" && cr.IsActive);

                    if (chatRoom == null)
                    {
                        // Create new chat room
                        var targetUser = await _context.UserAccounts.FirstOrDefaultAsync(u => u.Id == targetUserIdString);
                        if (targetUser == null)
                        {
                            await Clients.Caller.SendAsync("Error", "Target user not found");
                            return;
                        }

                        chatRoom = new ChatRoom
                        {
                            Id = Guid.NewGuid(),
                            User1Id = userId,
                            User2Id = targetUserIdString,
                            RoomType = "General",
                            CreatedAt = DateTime.UtcNow.ToLocalTime(),
                            LastActivityAt = DateTime.UtcNow.ToLocalTime(),
                            IsActive = true
                        };

                        _context.ChatRooms.Add(chatRoom);
                        await _context.SaveChangesAsync();
                        
                        actualChatRoomId = chatRoom.Id.ToString();
                        Console.WriteLine($"Created new chat room: {actualChatRoomId}");
                        
                        // Join the new room
                        var newRoomName = $"chat_{actualChatRoomId}";
                        await Groups.AddToGroupAsync(Context.ConnectionId, newRoomName);
                        
                        // Also add the target user to the group if they're online
                        string targetUserConnectionId = null;
                        lock (_lockObject)
                        {
                            UserConnections.TryGetValue(targetUserIdString, out targetUserConnectionId);
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
                        
                        // Notify the caller about the new chat room
                        await Clients.Caller.SendAsync("ChatRoomCreated", actualChatRoomId);
                    }
                    else
                    {
                        actualChatRoomId = chatRoom.Id.ToString();
                        Console.WriteLine($"Found existing chat room: {actualChatRoomId}");
                    }
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
                    
                    Console.WriteLine($"Using existing chat room: {actualChatRoomId}");
                }

                // Generate encryption key for this room
                var encryptionKey = _encryptionService.GenerateRoomKey(actualChatRoomId);

                // Encrypt the message before storing
                var encryptedMessage = _encryptionService.EncryptMessage(message, encryptionKey);

                // Save message to database
                var chatMessage = new ChatMessage
                {
                    Id = Guid.NewGuid(),
                    ChatRoomId = chatRoom.Id,
                    SenderId = userId,
                    Message = encryptedMessage,
                    MessageType = messageType,
                    SentAt = DateTime.UtcNow.ToLocalTime(),
                    IsRead = false
                };

                _context.ChatMessages.Add(chatMessage);
                
                // Update last activity
                chatRoom.LastActivityAt = DateTime.UtcNow.ToLocalTime();
                
                await _context.SaveChangesAsync();

                // Send to all users in the room
                var roomName = $"chat_{actualChatRoomId}";
                var messageObject = new
                {
                    Id = chatMessage.Id.ToString(),
                    SenderId = userId,
                    SenderName = fullName,
                    Message = message, // Send decrypted message to clients
                    MessageType = messageType,
                    SentAt = chatMessage.SentAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                    IsRead = false
                };
                
                Console.WriteLine($"Sending message to room {roomName}: {System.Text.Json.JsonSerializer.Serialize(messageObject)}");
                
                // Check if sender is in the group
                lock (_lockObject)
                {
                    if (RoomConnections.ContainsKey(roomName) && RoomConnections[roomName].Contains(Context.ConnectionId))
                    {
                        Console.WriteLine($"Sender {userId} is in room {roomName}");
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Sender {userId} is NOT in room {roomName}");
                        // Add sender to the group if not already there
                        Groups.AddToGroupAsync(Context.ConnectionId, roomName).Wait();
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
                
                await Clients.Group(roomName).SendAsync("ReceiveMessage", messageObject);

                // Update notification count for other user
                var otherUserId = chatRoom.User1Id == userId ? chatRoom.User2Id : chatRoom.User1Id;
                var unreadCount = await _context.ChatMessages
                    .CountAsync(m => m.ChatRoomId == chatRoom.Id && 
                                    m.SenderId != otherUserId && 
                                    !m.IsRead && 
                                    !m.IsDeleted);
                await UpdateNotificationCount(Context.GetHttpContext().RequestServices.GetRequiredService<IHubContext<ChatHub>>(), otherUserId, unreadCount);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendMessage: {ex.Message}");
                await Clients.Caller.SendAsync("Error", $"Failed to send message: {ex.Message}");
            }
        }

        public async Task SendFile(string chatRoomId, string fileName, string fileUrl, long fileSize, string fileType)
        {
            try
            {
                var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                var userId = userIdClaim;
                var user = await _context.UserAccounts.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                {
                    await Clients.Caller.SendAsync("Error", "User not found");
                    return;
                }

                var fullName = $"{user.FirstName} {user.LastName}";

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

                // Generate encryption key for this room
                var encryptionKey = _encryptionService.GenerateRoomKey(chatRoomId);

                // Encrypt the file name before storing (optional)
                var encryptedFileName = fileName;
                try
                {
                    encryptedFileName = _encryptionService.EncryptMessage(fileName, encryptionKey);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to encrypt filename: {ex.Message}");
                }

                // Determine message type based on file type
                string messageType = "file";
                if (fileType != null)
                {
                    if (fileType.StartsWith("image/"))
                    {
                        messageType = "image";
                    }
                    else if (fileType.StartsWith("video/"))
                    {
                        messageType = "video";
                    }
                }
                else
                {
                    // Fallback to file extension check
                    var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
                    if (new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" }.Contains(fileExtension))
                    {
                        messageType = "image";
                    }
                    else if (new[] { ".mp4", ".mov", ".avi", ".wmv", ".flv", ".webm" }.Contains(fileExtension))
                    {
                        messageType = "video";
                    }
                }

                // Save file message to database
                var chatMessage = new ChatMessage
                {
                    Id = Guid.NewGuid(),
                    ChatRoomId = chatRoom.Id,
                    SenderId = userId,
                    Message = encryptedFileName, // Store encrypted filename as message
                    MessageType = messageType,
                    FileUrl = fileUrl,
                    FileType = fileType,
                    FileSize = fileSize,
                    SentAt = DateTime.UtcNow.ToLocalTime(),
                    IsRead = false
                };

                _context.ChatMessages.Add(chatMessage);
                
                // Update last activity
                chatRoom.LastActivityAt = DateTime.UtcNow.ToLocalTime();
                
                await _context.SaveChangesAsync();

                // Send to all users in the room
                var roomName = $"chat_{chatRoomId}";
                var fileMessageObject = new
                {
                    Id = chatMessage.Id.ToString(),
                    SenderId = userId,
                    SenderName = fullName,
                    FileName = fileName, // Send original filename to clients
                    FileUrl = fileUrl,
                    FileType = fileType,
                    FileSize = fileSize,
                    MessageType = messageType,
                    SentAt = chatMessage.SentAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                    IsRead = false
                };
                
                Console.WriteLine($"Sending file message to room {roomName}: {System.Text.Json.JsonSerializer.Serialize(fileMessageObject)}");
                
                await Clients.Group(roomName).SendAsync("ReceiveFile", fileMessageObject);

                // Update notification count for other user
                var otherUserId = chatRoom.User1Id == userId ? chatRoom.User2Id : chatRoom.User1Id;
                var unreadCount = await _context.ChatMessages
                    .CountAsync(m => m.ChatRoomId == chatRoom.Id && 
                                    m.SenderId != otherUserId && 
                                    !m.IsRead && 
                                    !m.IsDeleted);
                await UpdateNotificationCount(Context.GetHttpContext().RequestServices.GetRequiredService<IHubContext<ChatHub>>(), otherUserId, unreadCount);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendFile: {ex.Message}");
                await Clients.Caller.SendAsync("Error", $"Failed to send file: {ex.Message}");
            }
        }

        public async Task MarkAsRead(string chatRoomId)
        {
            try
            {
                var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                var userId = userIdClaim;

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

                // Mark messages as read
                var unreadMessages = await _context.ChatMessages
                    .Where(m => m.ChatRoomId == chatRoom.Id && 
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

                // Notify other users that messages were read
                var roomName = $"chat_{chatRoomId}";
                await Clients.OthersInGroup(roomName).SendAsync("MessagesRead", userId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in MarkAsRead: {ex.Message}");
                await Clients.Caller.SendAsync("Error", "Failed to mark messages as read");
            }
        }

        public async Task Typing(string chatRoomId, bool isTyping)
        {
            try
            {
                var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return;
                }

                var userId = userIdClaim;

                // Verify access to this chat room
                var chatRoom = await _context.ChatRooms
                    .FirstOrDefaultAsync(cr => cr.Id.ToString() == chatRoomId &&
                                             (cr.User1Id == userId || cr.User2Id == userId) &&
                                             cr.IsActive);

                if (chatRoom == null)
                {
                    return;
                }

                var roomName = $"chat_{chatRoomId}";
                await Clients.OthersInGroup(roomName).SendAsync("UserTyping", userId, isTyping);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Typing: {ex.Message}");
            }
        }

        // Video Call Methods
        public async Task StartVideoCall(string chatRoomId)
        {
            try
            {
                var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                var userId = userIdClaim;
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
                    // For temporary chat rooms, we need to get the target user ID from the caller
                    // This will be handled by the video call page when it opens
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

                // Notify the caller that their call is being requested
                await Clients.Caller.SendAsync("CallRequested", new
                {
                    ChatRoomId = chatRoomId,
                    CallerId = userId,
                    CallerName = fullName ?? "Unknown User",
                    CallerPhoto = !string.IsNullOrEmpty(user.Photo) ? user.Photo : "https://ik.imagekit.io/6txj3mofs/GIGHub%20(11).png?updatedAt=1750552804497"
                });

                // Send to chat room (for users currently in chat)
                await Clients.OthersInGroup(roomName).SendAsync("IncomingVideoCall", new
                {
                    CallerId = userId,
                    CallerName = fullName ?? "Unknown User",
                    CallerPhoto = !string.IsNullOrEmpty(user.Photo) ? user.Photo : "https://ik.imagekit.io/6txj3mofs/GIGHub%20(11).png?updatedAt=1750552804497",
                    ChatRoomId = chatRoomId
                });

                // Also send to the partner's personal room (for global notifications)
                var partnerId = chatRoom.User1Id == userId ? chatRoom.User2Id : chatRoom.User1Id;
                var partnerRoomName = $"user_{partnerId}";
                
                await Clients.Group(partnerRoomName).SendAsync("IncomingVideoCall", new
                {
                    CallerId = userId.ToString(),
                    CallerName = fullName ?? "Unknown User",
                    CallerPhoto = !string.IsNullOrEmpty(user.Photo) ? user.Photo : "https://ik.imagekit.io/6txj3mofs/GIGHub%20(11).png?updatedAt=1750552804497",
                    ChatRoomId = chatRoomId
                });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", "Failed to start video call");
            }
        }

        public async Task AcceptVideoCall(string chatRoomId, string callerId)
        {
            try
            {
                var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                var userId = userIdClaim;

                // Check if this is a temporary chat room ID
                if (chatRoomId.StartsWith("temp_"))
                {
                    // For temporary chat rooms, we need to create a real chat room
                    // Get the caller's user info to create the chat room
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
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.ChatRooms.Add(newChatRoom);
                    await _context.SaveChangesAsync();

                    // Notify the caller that their call was accepted with the new chat room ID
                    await Clients.User(callerId).SendAsync("CallAccepted", new
                    {
                        ChatRoomId = newChatRoom.Id.ToString(),
                        AccepterId = userId,
                        IsTemporary = false
                    });

                    // Also notify the accepter
                    await Clients.Caller.SendAsync("CallAccepted", new
                    {
                        ChatRoomId = newChatRoom.Id.ToString(),
                        AccepterId = userId,
                        IsTemporary = false
                    });
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

                // Notify the caller that their call was accepted
                await Clients.User(callerId).SendAsync("CallAccepted", new
                {
                    ChatRoomId = chatRoomId,
                    AccepterId = userId
                });

                await Clients.Group(roomName).SendAsync("VideoCallAccepted", new
                {
                    AccepterId = userId,
                    CallerId = callerId,
                    ChatRoomId = chatRoomId
                });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", "Failed to accept video call");
            }
        }

        public async Task DeclineVideoCall(string chatRoomId, string callerId)
        {
            try
            {
                var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                var userId = userIdClaim;

                // Check if this is a temporary chat room ID
                if (chatRoomId.StartsWith("temp_"))
                {
                    // For temporary chat rooms, just notify the caller that their call was declined
                    await Clients.User(callerId).SendAsync("CallDeclined", new
                    {
                        ChatRoomId = chatRoomId,
                        DeclinerId = userId,
                        IsTemporary = true
                    });
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
                await Clients.User(callerId).SendAsync("CallDeclined", new
                {
                    ChatRoomId = chatRoomId,
                    DeclinerId = userId
                });

                await Clients.Group(roomName).SendAsync("VideoCallDeclined", new
                {
                    ChatRoomId = chatRoomId
                });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", "Failed to decline video call");
            }
        }

        public async Task EndVideoCall(string chatRoomId)
        {
            try
            {
                var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                var userId = userIdClaim;

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

                await Clients.Group(roomName).SendAsync("VideoCallEnded", new
                {
                    ChatRoomId = chatRoomId
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in EndVideoCall: {ex.Message}");
                await Clients.Caller.SendAsync("Error", "Failed to end video call");
            }
        }

        // WebRTC signaling methods
        public async Task SendOffer(string chatRoomId, string offer)
        {
            try
            {
                var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                var userId = userIdClaim;

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
                await Clients.OthersInGroup(roomName).SendAsync("ReceiveOffer", offer);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendOffer: {ex.Message}");
                await Clients.Caller.SendAsync("Error", "Failed to send offer");
            }
        }

        public async Task SendAnswer(string chatRoomId, string answer)
        {
            try
            {
                var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                var userId = userIdClaim;

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
                await Clients.OthersInGroup(roomName).SendAsync("ReceiveAnswer", answer);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendAnswer: {ex.Message}");
                await Clients.Caller.SendAsync("Error", "Failed to send answer");
            }
        }

        public async Task SendIceCandidate(string chatRoomId, string candidate)
        {
            try
            {
                var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                var userId = userIdClaim;

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
                await Clients.OthersInGroup(roomName).SendAsync("ReceiveIceCandidate", candidate);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendIceCandidate: {ex.Message}");
                await Clients.Caller.SendAsync("Error", "Failed to send ICE candidate");
            }
        }
    }
}
