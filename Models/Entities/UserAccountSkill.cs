﻿namespace Freelancing.Models.Entities
{
    public class UserAccountSkill
    {
        public Guid UserAccountId { get; set; }
        public Guid UserSkillId { get; set; }

        public virtual UserAccount UserAccount { get; set; }
        public virtual UserSkill UserSkill { get; set; }
    }
}
