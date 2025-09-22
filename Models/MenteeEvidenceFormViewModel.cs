using System.ComponentModel.DataAnnotations;

namespace Freelancing.Models
{
    public class MenteeEvidenceFormViewModel
    {
        public Guid? Id { get; set; }

        public Guid MatchId { get; set; }
        public Guid GoalId { get; set; }
        public string GoalName { get; set; }
        public string GoalDescription { get; set; }

        [Required(ErrorMessage = "Please describe what you accomplished during this session.")]
        [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters.")]
        [Display(Name = "What did you accomplish during this session?")]
        public string WhatWasDone { get; set; }

        [StringLength(5000, ErrorMessage = "Additional notes cannot exceed 5000 characters.")]
        [Display(Name = "Additional Notes (Optional)")]
        public string? AdditionalNotes { get; set; }

        [Display(Name = "Upload Evidence Files (Optional)")]
        public List<IFormFile>? EvidenceFiles { get; set; }

        public List<string>? RetainedEvidenceFilePaths { get; set; }
        public List<string>? ExistingEvidenceFilePaths { get; set; }

        [Display(Name = "Is Task Assigned")]
        public bool IsTaskAssigned { get; set; }

        [StringLength(1000)]
        [Display(Name = "Assigned Task Title")]
        public string? MentorTaskTitle { get; set; }

        [StringLength(5000)]
        [Display(Name = "Assigned Task Description")]
        public string? MentorTaskDescription { get; set; }

        public bool IsCompletedByMentor { get; set; }
        public bool IsCompletedByMentee { get; set; }
        public bool IsFullyCompleted { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}