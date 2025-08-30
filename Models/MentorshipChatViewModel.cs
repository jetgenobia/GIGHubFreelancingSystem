using Freelancing.Models.Entities;

namespace Freelancing.Models
{
    public class MentorshipChatViewModel
    {
        public Guid MatchId { get; set; }
        public string Status { get; set; }
        public UserAccount Partner { get; set; }
        public string CurrentUserId { get; set; }
        public List<ChatMessageViewModel> Messages { get; set; } = new();
        public bool IsCurrentUserMentor { get; set; }
    }

    public class ChatMessageViewModel
    {
        public Guid Id { get; set; }
        public string SenderId { get; set; }
        public string SenderName { get; set; }
        public string Message { get; set; }
        public string MessageType { get; set; }
        public string? FileUrl { get; set; }
        public string? FileType { get; set; }
        public long? FileSize { get; set; }
        public DateTime SentAt { get; set; }
        public bool IsRead { get; set; }
        public bool IsCurrentUser { get; set; }
    }

    public class VideoCallViewModel
    {
        public Guid MatchId { get; set; }
        public UserAccount Partner { get; set; }
        public string CurrentUserId { get; set; }
        public bool IsCurrentUserMentor { get; set; }
    }

    // For dashboard showing recent chats
    public class RecentChatViewModel
    {
        public Guid MatchId { get; set; }
        public UserAccount Partner { get; set; }
        public string LastMessage { get; set; }
        public DateTime LastMessageTime { get; set; }
        public int UnreadCount { get; set; }
        public bool IsOnline { get; set; }
    }
}