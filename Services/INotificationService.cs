using Freelancing.Models.Entities;

namespace Freelancing.Services
{
    public interface INotificationService
    {
        Task<Notification> CreateNotificationAsync(string userId, string title, string message, string type, string? iconSvg = null, string? relatedUrl = null, bool encryptContent = false);
        Task<List<Notification>> GetUserNotificationsAsync(string userId, int count = 10);
        Task<int> GetUnreadNotificationCountAsync(string userId);
        Task MarkNotificationAsReadAsync(Guid notificationId);
        Task MarkAllNotificationsAsReadAsync(string userId);
        Task<Notification> GetNotificationByIdAsync(Guid notificationId);
    }
}

