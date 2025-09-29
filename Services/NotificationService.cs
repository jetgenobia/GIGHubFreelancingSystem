using Freelancing.Data;
using Freelancing.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Freelancing.Hubs;

namespace Freelancing.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<MentorshipChatHub> _hubContext;
        private readonly IMessageEncryptionService _encryptionService;

        public NotificationService(ApplicationDbContext context, IHubContext<MentorshipChatHub> hubContext, IMessageEncryptionService encryptionService)
        {
            _context = context;
            _hubContext = hubContext;
            _encryptionService = encryptionService;
        }

        public async Task<Notification> CreateNotificationAsync(string userId, string title, string message, string type, string? iconSvg = null, string? relatedUrl = null, bool encryptContent = false)
        {
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                IconSvg = iconSvg,
                RelatedUrl = relatedUrl,
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                IsEncrypted = encryptContent,
                EncryptionMethod = encryptContent ? "AES-256" : null
            };

            // Encrypt content if requested
            if (encryptContent)
            {
                var encryptionKey = _encryptionService.GenerateRoomKey(userId.ToString());
                notification.EncryptedTitle = _encryptionService.EncryptMessage(title, encryptionKey);
                notification.EncryptedMessage = _encryptionService.EncryptMessage(message, encryptionKey);
                
                // Clear plain text content
                notification.Title = "[ENCRYPTED]";
                notification.Message = "[ENCRYPTED]";
            }

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // Broadcast the notification in real-time (decrypt if needed)
            var notificationForBroadcast = await DecryptNotificationIfNeeded(notification);
            await MentorshipChatHub.BroadcastNotification(_hubContext, notificationForBroadcast);

            // Update notification count
            var unreadCount = await GetUnreadNotificationCountAsync(userId);
            await MentorshipChatHub.UpdateNotificationCount(_hubContext, userId, unreadCount);

            return notification;
        }

        public async Task<List<Notification>> GetUserNotificationsAsync(string userId, int count = 10)
        {
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(count)
                .ToListAsync();

            // Decrypt notifications if needed
            var decryptedNotifications = new List<Notification>();
            foreach (var notification in notifications)
            {
                decryptedNotifications.Add(await DecryptNotificationIfNeeded(notification));
            }

            return decryptedNotifications;
        }

        public async Task<int> GetUnreadNotificationCountAsync(string userId)
        {
            return await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);
        }

        public async Task MarkNotificationAsReadAsync(Guid notificationId)
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification != null)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Update notification count in real-time
                var unreadCount = await GetUnreadNotificationCountAsync(notification.UserId);
                await MentorshipChatHub.UpdateNotificationCount(_hubContext, notification.UserId, unreadCount);
            }
        }

        public async Task MarkAllNotificationsAsReadAsync(string userId)
        {
            var unreadNotifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Update notification count in real-time
            var unreadCount = await GetUnreadNotificationCountAsync(userId);
            await MentorshipChatHub.UpdateNotificationCount(_hubContext, userId, unreadCount);
        }

        public async Task<Notification> GetNotificationByIdAsync(Guid notificationId)
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification != null)
            {
                return await DecryptNotificationIfNeeded(notification);
            }
            return null;
        }

        private async Task<Notification> DecryptNotificationIfNeeded(Notification notification)
        {
            if (!notification.IsEncrypted || string.IsNullOrEmpty(notification.EncryptedTitle) || string.IsNullOrEmpty(notification.EncryptedMessage))
            {
                return notification;
            }

            try
            {
                var encryptionKey = _encryptionService.GenerateRoomKey(notification.UserId.ToString());
                
                // Create a copy to avoid modifying the original entity
                var decryptedNotification = new Notification
                {
                    Id = notification.Id,
                    UserId = notification.UserId,
                    Title = _encryptionService.DecryptMessage(notification.EncryptedTitle, encryptionKey),
                    Message = _encryptionService.DecryptMessage(notification.EncryptedMessage, encryptionKey),
                    Type = notification.Type,
                    IconSvg = notification.IconSvg,
                    RelatedUrl = notification.RelatedUrl,
                    CreatedAt = notification.CreatedAt,
                    IsRead = notification.IsRead,
                    ReadAt = notification.ReadAt,
                    IsEncrypted = notification.IsEncrypted,
                    EncryptionMethod = notification.EncryptionMethod,
                    EncryptedTitle = notification.EncryptedTitle,
                    EncryptedMessage = notification.EncryptedMessage
                };

                return decryptedNotification;
            }
            catch (Exception ex)
            {
                // If decryption fails, return the original notification with encrypted indicators
                Console.WriteLine($"Failed to decrypt notification {notification.Id}: {ex.Message}");
                return notification;
            }
        }
    }
}

