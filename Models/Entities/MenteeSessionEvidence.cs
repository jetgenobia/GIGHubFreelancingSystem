using System.ComponentModel.DataAnnotations;

namespace Freelancing.Models.Entities
{
    public class MenteeSessionEvidence
    {
        public Guid Id { get; set; }

        [Required]
        public Guid MentorshipMatchId { get; set; }

        [Required]
        public Guid GoalId { get; set; }

        [Required]
        public string UserId { get; set; }

        [Required]
        [StringLength(2000)]
        public string WhatWasDone { get; set; }

        [StringLength(5000)]
        public string? AdditionalNotes { get; set; }

        // File paths for evidence (images, documents, etc.)
        public string? EvidenceFilePaths { get; set; }

        public DateTime SubmittedAt { get; set; }

        // Navigation properties
        public virtual MentorshipMatch MentorshipMatch { get; set; }
        public virtual Goal Goal { get; set; }
        public virtual UserAccount User { get; set; }
    }
}