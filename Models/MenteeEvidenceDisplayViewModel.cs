using Freelancing.Models.Entities;

namespace Freelancing.Models
{
    public class MenteeEvidenceDisplayViewModel
    {
        public Guid MatchId { get; set; }
        public Guid GoalId { get; set; }
        public string GoalName { get; set; } = "";
        public string MenteeName { get; set; } = "";
        public List<MenteeSessionEvidence> Evidences { get; set; } = new List<MenteeSessionEvidence>();

        public bool IsTaskAssigned { get; set; }
        public string? TaskTitle { get; set; }
        public string? TaskDescription { get; set; }

        public bool IsCompletedByMentor { get; set; }
        public bool IsCompletedByMentee { get; set; }
        public bool IsFullyCompleted { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}