using System.ComponentModel.DataAnnotations;

namespace Freelancing.Models.Entities
{
    public class MentorshipMatch
    {
        public Guid Id { get; set; }

        [Required]
        public string MentorId { get; set; }
        public UserAccount Mentor { get; set; }

        [Required]
        public string MenteeId { get; set; }
        public UserAccount Mentee { get; set; }

        public Guid MentorMentorshipId { get; set; }
        public PeerMentorship MentorMentorship { get; set; }

        public Guid MenteeMentorshipId { get; set; }
        public PeerMentorship MenteeMentorship { get; set; }

        [Required]
        public DateTime MatchedDate { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } // Active, Completed, Cancelled

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? DeclinedDate { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }
    }
}