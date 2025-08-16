using Freelancing.Models.Entities;

namespace Freelancing.Models
{
    public class ChatViewModel
    {
        public Guid ChatRoomId { get; set; }
        public string RoomName { get; set; }
        public string RoomType { get; set; }
        public UserAccount Partner { get; set; }
        public Guid CurrentUserId { get; set; }
        public List<ChatMessageViewModel> Messages { get; set; } = new();
        public MentorshipMatch MentorshipMatch { get; set; } // For mentorship chats
        public Guid? TargetUserId { get; set; } // For creating new chat rooms
    }

    public class ChatListItemViewModel
    {
        public Guid ChatRoomId { get; set; }
        public string RoomName { get; set; }
        public string RoomType { get; set; }
        public UserAccount Partner { get; set; }
        public string LastMessage { get; set; }
        public DateTime LastMessageTime { get; set; }
        public int UnreadCount { get; set; }
        public bool IsActive { get; set; }
        public MentorshipMatch MentorshipMatch { get; set; }
    }
}
