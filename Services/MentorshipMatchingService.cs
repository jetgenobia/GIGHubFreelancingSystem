using Freelancing.Data;
using Freelancing.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Freelancing.Services
{
    public interface IMentorshipMatchingService
    {
        Task<List<UserAccount>> FindPotentialMentorsAsync(string menteeId);
        Task<bool> FixExistingMentorshipMatchesAsync();
    }

    public class MentorshipMatchingService : IMentorshipMatchingService
    {
        private readonly ApplicationDbContext _context;

        public MentorshipMatchingService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Find potential mentors for a mentee based on 100% skill match
        public async Task<List<UserAccount>> FindPotentialMentorsAsync(string menteeId)
        {
            // Get mentee's skills
            var menteeSkills = await _context.UserAccountSkills
                .Where(uas => uas.UserAccountId == menteeId)
                .Select(uas => uas.UserSkillId)
                .ToListAsync();

            if (!menteeSkills.Any())
                return new List<UserAccount>();

            // Get mentee's mentorship info to ensure they're registered as mentee
            var menteeMentorship = await _context.PeerMentorships
                .FirstOrDefaultAsync(pm => pm.UserId == menteeId && pm.Role.ToLower() == "mentee");

            if (menteeMentorship == null)
                return new List<UserAccount>();

            // Find users who are registered as mentors and have ALL the same skills as the mentee
            var potentialMentors = await _context.UserAccounts
                .Include(ua => ua.UserAccountSkills)
                .ThenInclude(uas => uas.UserSkill)
                .Include(ua => ua.Mentorship)
                .Where(ua => ua.Mentorship != null &&
                           ua.Mentorship.Role.ToLower() == "mentor" &&
                           ua.Id != menteeId && // Exclude the mentee themselves
                           ua.UserAccountSkills.Count > 0 && // Must have skills
                           ua.UserAccountSkills.Select(uas => uas.UserSkillId)
                               .Any(skillId => menteeSkills.Contains(skillId)) && // All mentor skills must be in mentee skills
                           menteeSkills.Any(skillId => ua.UserAccountSkills
                               .Select(uas => uas.UserSkillId).Contains(skillId))) // All mentee skills must be in mentor skills
                .ToListAsync();

            return potentialMentors;
        }
        // Create a mentorship match
        public async Task<bool> CreateMatchAsync(string mentorId, string menteeId)
        {
            // Verify both users are in mentorship program with correct roles
            var mentor = await _context.PeerMentorships
                .FirstOrDefaultAsync(pm => pm.UserId == mentorId && pm.Role.ToLower() == "mentor");
            var mentee = await _context.PeerMentorships
                .FirstOrDefaultAsync(pm => pm.UserId == menteeId && pm.Role.ToLower() == "mentee");

            if (mentor == null || mentee == null)
                return false;

            // Check if match already exists
            var existingMatch = await _context.MentorshipMatches
                .FirstOrDefaultAsync(pm => pm.MentorId == mentorId && pm.MenteeId == menteeId);

            if (existingMatch != null)
                return false;

            // Verify 100% skill match
            var mentorSkills = await _context.UserAccountSkills
                .Where(uas => uas.UserAccountId == mentorId)
                .Select(uas => uas.UserSkillId)
                .OrderBy(id => id)
                .ToListAsync();

            var menteeSkills = await _context.UserAccountSkills
                .Where(uas => uas.UserAccountId == menteeId)
                .Select(uas => uas.UserSkillId)
                .OrderBy(id => id)
                .ToListAsync();

            // Ensure 100% match (same skills)
            if (!mentorSkills.SequenceEqual(menteeSkills))
                return false;

            // Create the match
            var match = new MentorshipMatch
            {
                MentorId = mentorId,
                MenteeId = menteeId,
                MentorMentorshipId = mentor.Id,
                MenteeMentorshipId = mentee.Id,
                MatchedDate = DateTime.UtcNow,
                Status = "Active"
            };

            _context.MentorshipMatches.Add(match);
            await _context.SaveChangesAsync();

            return true;
        }

        // Get all matches for a user (either as mentor or mentee)
        public async Task<List<MentorshipMatch>> GetUserMatchesAsync(string userId)
        {
            var matches = await _context.MentorshipMatches
                .Include(mm => mm.Mentor)
                .ThenInclude(m => m.UserAccountSkills)
                .ThenInclude(uas => uas.UserSkill)
                .Include(mm => mm.Mentee)
                .ThenInclude(m => m.UserAccountSkills)
                .ThenInclude(uas => uas.UserSkill)
                .Include(mm => mm.MentorMentorship)
                .Include(mm => mm.MenteeMentorship)
                .Where(mm => mm.MentorId == userId || mm.MenteeId == userId)
                .OrderByDescending(mm => mm.MatchedDate)
                .ToListAsync();

            return matches;
        }

        // Fix existing mentorship matches that are missing mentorship IDs
        public async Task<bool> FixExistingMentorshipMatchesAsync()
        {
            try
            {
                var matchesToFix = await _context.MentorshipMatches
                    .Where(mm => mm.MentorMentorshipId == Guid.Empty || mm.MenteeMentorshipId == Guid.Empty)
                    .ToListAsync();

                foreach (var match in matchesToFix)
                {
                    // Get the mentor's mentorship record
                    var mentorMentorship = await _context.PeerMentorships
                        .FirstOrDefaultAsync(pm => pm.UserId == match.MentorId && pm.Role.ToLower() == "mentor");
                    
                    // Get the mentee's mentorship record
                    var menteeMentorship = await _context.PeerMentorships
                        .FirstOrDefaultAsync(pm => pm.UserId == match.MenteeId && pm.Role.ToLower() == "mentee");

                    if (mentorMentorship != null && menteeMentorship != null)
                    {
                        match.MentorMentorshipId = mentorMentorship.Id;
                        match.MenteeMentorshipId = menteeMentorship.Id;
                    }
                }

                if (matchesToFix.Any())
                {
                    await _context.SaveChangesAsync();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fixing mentorship matches: {ex.Message}");
                return false;
            }
        }
    }
}