using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Freelancing.Data;
using Freelancing.Models.Entities;
using System.Security.Claims;

namespace Freelancing.Controllers
{
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProfileController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Freelancer(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            // Get freelancer profile with all related data
            var freelancer = await _context.UserAccounts
                .Include(u => u.UserAccountSkills)
                    .ThenInclude(uas => uas.UserSkill)
                .Include(u => u.Portfolios)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (freelancer == null)
                return NotFound();

            // Identity verification status
            var identityVerification = await _context.IdentityVerifications
                .AsNoTracking()
                .FirstOrDefaultAsync(iv => iv.UserAccountId == id);
            var isVerified = identityVerification?.IdDocumentVerified == true
                             && identityVerification?.FaceVerified == true;
            ViewBag.IsVerified = isVerified;
            ViewBag.IdentityVerification = identityVerification;

            // Check mentorship completion status
            var completedAsMentor = await _context.MentorshipMatches
                .AnyAsync(mm => mm.MentorId == id && mm.Status == "Completed");

            var completedAsMentee = await _context.MentorshipMatches
                .AnyAsync(mm => mm.MenteeId == id && mm.Status == "Completed");

            // Get user's projects and biddings for portfolio
            var projects = await _context.Projects
                .Where(p => p.UserId == id)
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .ToListAsync();

            var biddings = await _context.Biddings
                .Include(b => b.Project)
                .Where(b => b.UserId == id && b.IsAccepted == true)
                .Take(5)
                .ToListAsync();

            var feedbacks = await _context.FreelancerFeedbacks
                .Where(f => f.FreelancerId == id)
                .Include(f => f.AcceptBidding)
                    .ThenInclude(ab => ab.Project)
                        .ThenInclude(p => p.User)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            var mentorfeedback = await _context.MentorReviews
                .Where(f => f.MentorId == id)
                .Include(f => f.MentorshipMatch)
                    .ThenInclude(mm => mm.Mentee)
                .Include(f => f.Mentee)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            var profileOwnerRole = freelancer?.Role ?? freelancer?.FRole;
            var isMentor = !string.IsNullOrEmpty(profileOwnerRole) &&
                           profileOwnerRole.Equals("Mentor", StringComparison.OrdinalIgnoreCase);

            if (!isMentor)
            {
                isMentor = await _context.MentorshipMatches.AnyAsync(mm => mm.MentorId == id);
            }

            ViewBag.IsMentor = isMentor;

            var feedbackDtos = feedbacks.Select(f => new FeedbackDto
            {
                Id = f.Id,
                Rating = f.Rating,
                WouldRecommend = f.WouldRecommend,
                Comments = f.Comments,
                CreatedAt = f.CreatedAt,
                User = new UserDto
                {
                    Id = f.AcceptBidding.Project.User.Id,
                    FirstName = f.AcceptBidding.Project.User.FirstName,
                    LastName = f.AcceptBidding.Project.User.LastName,
                    Email = f.AcceptBidding.Project.User.Email,
                    UserName = f.AcceptBidding.Project.User.UserName,
                    Photo = f.AcceptBidding.Project.User.Photo
                }
            }).ToList();

            // Calculate average rating
            var averageRating = feedbacks.Any() ? feedbacks.Average(f => f.Rating) : 0;

            // Calculate recommendation rate
            var recommendationRate = feedbacks.Any()
                ? (double)feedbacks.Count(f => f.WouldRecommend) / feedbacks.Count * 100
                : 0;

            ViewBag.MentorshipData = (completedAsMentor, completedAsMentee);
            ViewBag.Projects = projects;
            ViewBag.Biddings = biddings;
            ViewBag.AllFeedbacks = feedbacks;
            ViewBag.RecentFeedbacks = feedbacks;
            ViewBag.FeedbackDtos = feedbackDtos;
            ViewBag.AverageRating = averageRating;
            ViewBag.RecommendationRate = recommendationRate;
            ViewBag.FeedbackCount = feedbacks.Count;
            ViewBag.MFeedback = mentorfeedback;

            return View(freelancer);
        }

        public async Task<IActionResult> Client(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            // Get client profile with all related data
            var client = await _context.UserAccounts
                .Include(u => u.Projects)
                    .ThenInclude(p => p.Biddings)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (client == null)
                return NotFound();

            // Identity verification status
            var identityVerification = await _context.IdentityVerifications
                .AsNoTracking()
                .FirstOrDefaultAsync(iv => iv.UserAccountId == id);
            var isVerified = identityVerification?.IdDocumentVerified == true
                             && identityVerification?.FaceVerified == true;
            ViewBag.IsVerified = isVerified;
            ViewBag.IdentityVerification = identityVerification;

            // Get client's projects
            var projects = await _context.Projects
                .Where(p => p.UserId == id)
                .OrderByDescending(p => p.CreatedAt)
                .Take(10)
                .ToListAsync();

            ViewBag.Projects = projects;

            return View(client);
        }
    }
}