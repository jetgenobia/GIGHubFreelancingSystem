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
    public class MentorshipChatHub : Hub
    {
        private readonly ApplicationDbContext _context;
        private readonly IMessageEncryptionService _encryptionService;
        private static readonly Dictionary<string, string> UserConnections = new();
        private static readonly Dictionary<string, List<string>> RoomConnections = new();
        private static readonly object _lockObject = new object();

        public MentorshipChatHub(ApplicationDbContext context, IMessageEncryptionService encryptionService)
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

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                lock (_lockObject)
                {
                    UserConnections.Remove(userId);

                    // Remove from all rooms
                    var roomsToRemove = RoomConnections
                        .Where(kvp => kvp.Value.Contains(Context.ConnectionId))
                        .Select(kvp => kvp.Key)
                        .ToList();

                    foreach (var room in roomsToRemove)
                    {
                        RoomConnections[room].Remove(Context.ConnectionId);
                        if (!RoomConnections[room].Any())
                        {
                            RoomConnections.Remove(room);
                        }
                    }
                }

                // Remove from SignalR groups outside the lock
                var roomsToRemoveForGroups = RoomConnections
                    .Where(kvp => kvp.Value.Contains(Context.ConnectionId))
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var room in roomsToRemoveForGroups)
                {
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, room);
                }
            }
            await base.OnDisconnectedAsync(exception);
        }

        // Add method to broadcast notifications
        public static async Task BroadcastNotification(IHubContext<MentorshipChatHub> hubContext, Notification notification)
        {
            string connectionId = null;
            lock (_lockObject)
            {
                UserConnections.TryGetValue(notification.UserId.ToString(), out connectionId);
            }
            
            if (!string.IsNullOrEmpty(connectionId))
            {
                await hubContext.Clients.Client(connectionId).SendAsync("ReceiveNotification", notification);
            }
        }

        // Add method to update notification count
        public static async Task UpdateNotificationCount(IHubContext<MentorshipChatHub> hubContext, string userId, int count)
        {
            string connectionId = null;
            lock (_lockObject)
            {
                UserConnections.TryGetValue(userId.ToString(), out connectionId);
            }
            
            if (!string.IsNullOrEmpty(connectionId))
            {
                await hubContext.Clients.Client(connectionId).SendAsync("UpdateNotificationCount", count);
            }
        }

        public async Task JoinMentorshipRoom(string mentorshipMatchId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // Verify user is part of this mentorship match
            var match = await _context.MentorshipMatches
                .FirstOrDefaultAsync(mm => mm.Id.ToString() == mentorshipMatchId &&
                                          (mm.MentorId == userId || mm.MenteeId == userId) &&
                                          (mm.Status == "Active" || mm.Status == "Completed"));

            if (match == null)
            {
                await Clients.Caller.SendAsync("Error", "Access denied or mentorship not found");
                return;
            }

            var roomName = $"mentorship_{mentorshipMatchId}";
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

        public async Task SendMessage(string mentorshipMatchId, string message, string messageType = "text")
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

                // Verify access
                var match = await _context.MentorshipMatches
                    .FirstOrDefaultAsync(mm => mm.Id.ToString() == mentorshipMatchId &&
                                              (mm.MentorId == userId || mm.MenteeId == userId) &&
                                              (mm.Status == "Active" || mm.Status == "Completed"));

                if (match == null)
                {
                    await Clients.Caller.SendAsync("Error", "Access denied or match not found");
                    return;
                }

                // Generate encryption key for this room
                var encryptionKey = _encryptionService.GenerateRoomKey(mentorshipMatchId);

                // Encrypt the message before storing
                var encryptedMessage = _encryptionService.EncryptMessage(message, encryptionKey);

                // Save encrypted message to database
                var chatMessage = new MentorshipChatMessage
                {
                    Id = Guid.NewGuid(),
                    MentorshipMatchId = Guid.Parse(mentorshipMatchId),
                    SenderId = userId,
                    Message = encryptedMessage, // Store encrypted
                    MessageType = messageType,
                    SentAt = DateTime.UtcNow,
                    IsRead = false
                };

                _context.MentorshipChatMessages.Add(chatMessage);
                await _context.SaveChangesAsync();

                var roomName = $"mentorship_{mentorshipMatchId}";

                // Send decrypted message to clients for display
                var messageToSend = new
                {
                    Id = chatMessage.Id.ToString(),
                    SenderId = userId,
                    SenderName = fullName,
                    Message = message, // Send original message for display
                    MessageType = messageType,
                    SentAt = chatMessage.SentAt.ToString("O"),
                    IsRead = false,
                    IsEncrypted = false // Server-side encryption
                };

                await Clients.Group(roomName).SendAsync("ReceiveMessage", messageToSend);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendMessage: {ex.Message}");
                await Clients.Caller.SendAsync("Error", $"Failed to send message: {ex.Message}");
            }
        }

        // FIXED: Complete SendFile implementation with encryption support
        public async Task SendFile(string mentorshipMatchId, string fileName, string fileUrl, long fileSize, string fileType)
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

                // Verify access
                var match = await _context.MentorshipMatches
                    .FirstOrDefaultAsync(mm => mm.Id.ToString() == mentorshipMatchId &&
                                              (mm.MentorId == userId || mm.MenteeId == userId) &&
                                              (mm.Status == "Active" || mm.Status == "Completed"));

                if (match == null)
                {
                    await Clients.Caller.SendAsync("Error", "Access denied or match not found");
                    return;
                }

                // Generate encryption key for this room
                var encryptionKey = _encryptionService.GenerateRoomKey(mentorshipMatchId);

                // Encrypt the file name before storing (optional)
                var encryptedFileName = fileName;
                try
                {
                    encryptedFileName = _encryptionService.EncryptMessage(fileName, encryptionKey);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to encrypt filename: {ex.Message}");
                    // Continue with unencrypted filename if encryption fails
                }

                // Determine message type based on file type
                var messageType = DetermineMessageType(fileType, fileName);

                // Save encrypted file message to database
                var chatMessage = new MentorshipChatMessage
                {
                    Id = Guid.NewGuid(),
                    MentorshipMatchId = Guid.Parse(mentorshipMatchId),
                    SenderId = userId,
                    Message = encryptedFileName, // Store encrypted filename
                    MessageType = messageType,
                    FileUrl = fileUrl,
                    FileType = fileType,
                    FileSize = fileSize,
                    SentAt = DateTime.UtcNow,
                    IsRead = false
                };

                _context.MentorshipChatMessages.Add(chatMessage);
                await _context.SaveChangesAsync();

                var roomName = $"mentorship_{mentorshipMatchId}";

                // Send file message to clients
                var fileMessageToSend = new
                {
                    Id = chatMessage.Id.ToString(),
                    SenderId = userId,
                    SenderName = fullName,
                    Message = fileName, // Send original filename for display
                    FileName = fileName,
                    FileUrl = fileUrl,
                    FileSize = fileSize,
                    FileType = fileType,
                    MessageType = messageType,
                    SentAt = chatMessage.SentAt.ToString("O"),
                    IsRead = false,
                    IsEncrypted = false // Server-side encryption
                };

                await Clients.Group(roomName).SendAsync("ReceiveFile", fileMessageToSend);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendFile: {ex.Message}");
                await Clients.Caller.SendAsync("Error", $"Failed to send file: {ex.Message}");
            }
        }

        // Helper method to determine message type based on file
        private string DetermineMessageType(string fileType, string fileName)
        {
            if (string.IsNullOrEmpty(fileType))
            {
                // Fallback to file extension
                var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
                return extension switch
                {
                    ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" => "image",
                    ".mp4" or ".mov" or ".avi" or ".wmv" => "video",
                    _ => "file"
                };
            }

            if (fileType.StartsWith("image/"))
                return "image";
            if (fileType.StartsWith("video/"))
                return "video";

            return "file";
        }

        public async Task StartVideoCall(string mentorshipMatchId)
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

                // Verify access to this mentorship match
                var match = await _context.MentorshipMatches
                    .FirstOrDefaultAsync(mm => mm.Id.ToString() == mentorshipMatchId &&
                                              (mm.MentorId == userId || mm.MenteeId == userId) &&
                                              (mm.Status == "Active" || mm.Status == "Completed"));

                if (match == null)
                {
                    await Clients.Caller.SendAsync("Error", "Access denied or mentorship not found");
                    return;
                }

                var roomName = $"mentorship_{mentorshipMatchId}";

                // Notify the caller that their call is being requested
                await Clients.Caller.SendAsync("CallRequested", new
                {
                    MentorshipMatchId = mentorshipMatchId,
                    CallerId = userId,
                    CallerName = fullName ?? "Unknown User",
                    CallerPhoto = !string.IsNullOrEmpty(user.Photo) ? user.Photo : "https://ik.imagekit.io/6txj3mofs/GIGHub%20(11).png?updatedAt=1750552804497"
                });

                // Send to mentorship room (for users currently in chat)
                await Clients.OthersInGroup(roomName).SendAsync("IncomingVideoCall", new
                {
                    CallerId = userId,
                    CallerName = fullName ?? "Unknown User",
                    CallerPhoto = !string.IsNullOrEmpty(user.Photo) ? user.Photo : "https://ik.imagekit.io/6txj3mofs/GIGHub%20(11).png?updatedAt=1750552804497",
                    MentorshipMatchId = mentorshipMatchId
                });

                // Also send to the partner's personal room (for global notifications)
                var partnerId = match.MentorId == userId ? match.MenteeId : match.MentorId;
                var partnerRoomName = $"user_{partnerId}";
                
                await Clients.Group(partnerRoomName).SendAsync("IncomingVideoCall", new
                {
                    CallerId = userId,
                    CallerName = fullName ?? "Unknown User",
                    CallerPhoto = !string.IsNullOrEmpty(user.Photo) ? user.Photo : "https://ik.imagekit.io/6txj3mofs/GIGHub%20(11).png?updatedAt=1750552804497",
                    MentorshipMatchId = mentorshipMatchId
                });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", "Failed to start video call");
            }
        }

        public async Task AcceptVideoCall(string mentorshipMatchId, string callerId)
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

                // Verify access to this mentorship match
                var match = await _context.MentorshipMatches
                    .FirstOrDefaultAsync(mm => mm.Id.ToString() == mentorshipMatchId &&
                                              (mm.MentorId == userId || mm.MenteeId == userId) &&
                                              (mm.Status == "Active" || mm.Status == "Completed"));

                if (match == null)
                {
                    await Clients.Caller.SendAsync("Error", "Access denied or mentorship not found");
                    return;
                }

                var roomName = $"mentorship_{mentorshipMatchId}";

                // Notify the caller that their call was accepted
                await Clients.User(callerId).SendAsync("CallAccepted", new
                {
                    MentorshipMatchId = mentorshipMatchId,
                    AccepterId = userId
                });

                await Clients.Group(roomName).SendAsync("VideoCallAccepted", new
                {
                    AccepterId = userId,
                    CallerId = callerId,
                    MentorshipMatchId = mentorshipMatchId
                });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", "Failed to accept video call");
            }
        }

        public async Task DeclineVideoCall(string mentorshipMatchId, string callerId)
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

                // Verify access to this mentorship match
                var match = await _context.MentorshipMatches
                    .FirstOrDefaultAsync(mm => mm.Id.ToString() == mentorshipMatchId &&
                                              (mm.MentorId == userId || mm.MenteeId == userId) &&
                                              (mm.Status == "Active" || mm.Status == "Completed"));

                if (match == null)
                {
                    await Clients.Caller.SendAsync("Error", "Access denied or mentorship not found");
                    return;
                }

                var roomName = $"mentorship_{mentorshipMatchId}";

                // Notify the caller that their call was declined
                await Clients.User(callerId).SendAsync("CallDeclined", new
                {
                    MentorshipMatchId = mentorshipMatchId,
                    DeclinerId = userId
                });

                await Clients.Group(roomName).SendAsync("VideoCallDeclined", new
                {
                    MentorshipMatchId = mentorshipMatchId
                });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", "Failed to decline video call");
            }
        }

        public async Task EndVideoCall(string mentorshipMatchId)
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

                // Verify access to this mentorship match
                var match = await _context.MentorshipMatches
                    .FirstOrDefaultAsync(mm => mm.Id.ToString() == mentorshipMatchId &&
                                              (mm.MentorId == userId || mm.MenteeId == userId) &&
                                              (mm.Status == "Active" || mm.Status == "Completed"));

                if (match == null)
                {
                    await Clients.Caller.SendAsync("Error", "Access denied or mentorship not found");
                    return;
                }

                var roomName = $"mentorship_{mentorshipMatchId}";

                await Clients.Group(roomName).SendAsync("VideoCallEnded", new
                {
                    MentorshipMatchId = mentorshipMatchId
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in EndVideoCall: {ex.Message}");
                await Clients.Caller.SendAsync("Error", "Failed to end video call");
            }
        }

        // WebRTC signaling methods
        public async Task SendOffer(string mentorshipMatchId, string offer)
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

                // Verify access to this mentorship match
                var match = await _context.MentorshipMatches
                    .FirstOrDefaultAsync(mm => mm.Id.ToString() == mentorshipMatchId &&
                                              (mm.MentorId == userId || mm.MenteeId == userId) &&
                                              (mm.Status == "Active" || mm.Status == "Completed"));

                if (match == null)
                {
                    await Clients.Caller.SendAsync("Error", "Access denied or mentorship not found");
                    return;
                }

                var roomName = $"mentorship_{mentorshipMatchId}";
                await Clients.OthersInGroup(roomName).SendAsync("ReceiveOffer", offer);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendOffer: {ex.Message}");
                await Clients.Caller.SendAsync("Error", "Failed to send offer");
            }
        }

        public async Task SendAnswer(string mentorshipMatchId, string answer)
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

                // Verify access to this mentorship match
                var match = await _context.MentorshipMatches
                    .FirstOrDefaultAsync(mm => mm.Id.ToString() == mentorshipMatchId &&
                                              (mm.MentorId == userId || mm.MenteeId == userId) &&
                                              (mm.Status == "Active" || mm.Status == "Completed"));

                if (match == null)
                {
                    await Clients.Caller.SendAsync("Error", "Access denied or mentorship not found");
                    return;
                }

                var roomName = $"mentorship_{mentorshipMatchId}";
                await Clients.OthersInGroup(roomName).SendAsync("ReceiveAnswer", answer);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendAnswer: {ex.Message}");
                await Clients.Caller.SendAsync("Error", "Failed to send answer");
            }
        }

        public async Task SendIceCandidate(string mentorshipMatchId, string candidate)
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

                // Verify access to this mentorship match
                var match = await _context.MentorshipMatches
                    .FirstOrDefaultAsync(mm => mm.Id.ToString() == mentorshipMatchId &&
                                              (mm.MentorId == userId || mm.MenteeId == userId) &&
                                              (mm.Status == "Active" || mm.Status == "Completed"));

                if (match == null)
                {
                    await Clients.Caller.SendAsync("Error", "Access denied or mentorship not found");
                    return;
                }

                var roomName = $"mentorship_{mentorshipMatchId}";
                await Clients.OthersInGroup(roomName).SendAsync("ReceiveIceCandidate", candidate);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendIceCandidate: {ex.Message}");
                await Clients.Caller.SendAsync("Error", "Failed to send ICE candidate");
            }
        }

        public async Task MarkMessagesAsRead(string mentorshipMatchId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var messages = await _context.MentorshipChatMessages
                .Where(mcm => mcm.MentorshipMatchId.ToString() == mentorshipMatchId &&
                             mcm.SenderId != userId && !mcm.IsRead)
                .ToListAsync();

            foreach (var message in messages)
            {
                message.IsRead = true;
                message.ReadAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            var roomName = $"mentorship_{mentorshipMatchId}";
            await Clients.Group(roomName).SendAsync("MessagesMarkedAsRead", new
            {
                ReadById = userId,
                MessageIds = messages.Select(m => m.Id).ToList()
            });
        }

        public async Task SendTypingIndicator(string mentorshipMatchId, bool isTyping)
        {
            try
            {
                var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return; // Don't send error for typing indicator
                }

                var userId = userIdClaim;
                var user = await _context.UserAccounts.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                {
                    return; // Don't send error for typing indicator
                }

                var fullName = $"{user.FirstName} {user.LastName}";
                var roomName = $"mentorship_{mentorshipMatchId}";

                await Clients.OthersInGroup(roomName).SendAsync("TypingIndicator", new
                {
                    UserId = userId,
                    UserName = fullName,
                    IsTyping = isTyping
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendTypingIndicator: {ex.Message}");
                // Don't send error for typing indicator
            }
        }
    }
}