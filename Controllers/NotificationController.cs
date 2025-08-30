using Freelancing.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Freelancing.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly INotificationService _notificationService;

        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var notifications = await _notificationService.GetUserNotificationsAsync(userId, 10);
            return Json(notifications);
        }

        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var count = await _notificationService.GetUnreadNotificationCountAsync(userId);
            return Json(new { count });
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsRead(Guid notificationId)
        {
            await _notificationService.MarkNotificationAsReadAsync(notificationId);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            await _notificationService.MarkAllNotificationsAsReadAsync(userId);
            return Ok();
        }

        // Example method to create an encrypted notification (for demonstration)
        [HttpPost]
        public async Task<IActionResult> CreateEncryptedNotification(string title, string message, string type = "encrypted")
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Create an encrypted notification
            var notification = await _notificationService.CreateNotificationAsync(
                userId, 
                title, 
                message, 
                type, 
                encryptContent: true // This enables encryption
            );

            return Json(new { 
                success = true, 
                notificationId = notification.Id,
                message = "Encrypted notification created successfully" 
            });
        }

        // Example method to create a regular (non-encrypted) notification
        [HttpPost]
        public async Task<IActionResult> CreateRegularNotification(string title, string message, string type = "regular")
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Create a regular notification (not encrypted)
            var notification = await _notificationService.CreateNotificationAsync(
                userId, 
                title, 
                message, 
                type, 
                encryptContent: false // This keeps it unencrypted
            );

            return Json(new { 
                success = true, 
                notificationId = notification.Id,
                message = "Regular notification created successfully" 
            });
        }
    }
}

