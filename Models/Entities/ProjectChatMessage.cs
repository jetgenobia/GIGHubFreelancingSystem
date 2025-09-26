using System.ComponentModel.DataAnnotations;

namespace Freelancing.Models.Entities
{
    public class ChatMessage
    {
        public Guid Id { get; set; }

        [Required]
        public Guid ChatRoomId { get; set; }

        [Required]
        public string SenderId { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Message { get; set; }

        [Required]
        [MaxLength(20)]
        public string MessageType { get; set; } // "text", "file", "image", "video", "system"

        public string? FileUrl { get; set; }
        public string? FileType { get; set; }
        public long? FileSize { get; set; }

        public DateTime SentAt { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }

        // Navigation properties
        public virtual ChatRoom ChatRoom { get; set; }
        public virtual UserAccount Sender { get; set; }
    }

    public class ChatRoom
    {
        public Guid Id { get; set; }
        
        [Required]
        public string User1Id { get; set; }
        
        [Required]
        public string User2Id { get; set; }
        

        
        public string RoomType { get; set; } = "General"; // "General", "Project", "Mentorship"
        
        public Guid? ProjectId { get; set; } // For project-related chats
        public Guid? MentorshipMatchId { get; set; } // For mentorship chats
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastActivityAt { get; set; }
        public bool IsActive { get; set; } = true;
        
        // Navigation properties
        public virtual UserAccount User1 { get; set; }
        public virtual UserAccount User2 { get; set; }
        public virtual Project Project { get; set; }
        public virtual MentorshipMatch MentorshipMatch { get; set; }
        public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }

    // For storing file metadata
    public class ChatFile
    {
        public Guid Id { get; set; }

        [Required]
        public Guid MessageId { get; set; }

        [Required]
        [MaxLength(255)]
        public string OriginalFileName { get; set; }

        [Required]
        [MaxLength(500)]
        public string StoredFileName { get; set; }

        [Required]
        [MaxLength(500)]
        public string FilePath { get; set; }

        [Required]
        [MaxLength(100)]
        public string ContentType { get; set; }

        public long FileSize { get; set; }
        public DateTime UploadedAt { get; set; }

        // Navigation properties
        public virtual ChatMessage Message { get; set; }
    }
}
