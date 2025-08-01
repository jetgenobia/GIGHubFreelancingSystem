﻿using Freelancing.Models.Entities;

namespace Freelancing.Models
{
    public class EditAccount
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string UserName { get; set; }
        public string? Photo { get; set; }
        public List<UserSkill> SavedSkills { get; set; } = new List<UserSkill>();
        public Guid UserId { get; set; }
        public int TotalSkillsCount { get; set; }
    }
}
