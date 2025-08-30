using System.ComponentModel.DataAnnotations;

namespace Freelancing.Models.Entities
{
    public class MentorshipChatMessage
    {
        public Guid Id { get; set; }

        [Required]
        public Guid MentorshipMatchId { get; set; }

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
        public virtual MentorshipMatch MentorshipMatch { get; set; }
        public virtual UserAccount Sender { get; set; }
    }

    // For storing file metadata
    public class MentorshipChatFile
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
        public virtual MentorshipChatMessage Message { get; set; }
    }
}