namespace Freelancing.Models
{
    public class MentorNoteDisplay
    {
        public Guid? Id { get; set; }
        public Guid MatchId { get; set; }
        public Guid GoalId { get; set; }
        public string? GoalName { get; set; }
        public string? MentorName { get; set; }
        public string? MenteeName { get; set; }

        // Note contents
        public string? Notes { get; set; }
        public string? Feedback { get; set; }
        public int? ProgressRating { get; set; }

        // Task (optional)
        public bool IsTaskAssigned { get; set; }
        public string? TaskTitle { get; set; }
        public string? TaskDescription { get; set; }

        public DateTime? SubmittedAt { get; set; }
    }
}
