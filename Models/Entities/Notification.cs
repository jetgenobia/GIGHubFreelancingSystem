using System.ComponentModel.DataAnnotations;

namespace Freelancing.Models.Entities
{
    public class Notification
    {
        public Guid Id { get; set; }

        [Required]
        public string UserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Title { get; set; }

        [Required]
        [MaxLength(500)]
        public string Message { get; set; }

        [Required]
        [MaxLength(50)]
        public string Type { get; set; } // "registration", "message", "mentorship", etc.

        public string? IconSvg { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsRead { get; set; } = false;

        public DateTime? ReadAt { get; set; }

        public string? RelatedUrl { get; set; }

        // Encryption fields
        public bool IsEncrypted { get; set; } = false;
        public string? EncryptionMethod { get; set; }
        public string? EncryptedTitle { get; set; }
        public string? EncryptedMessage { get; set; }

        // Navigation property
        public virtual UserAccount User { get; set; }
    }
}

