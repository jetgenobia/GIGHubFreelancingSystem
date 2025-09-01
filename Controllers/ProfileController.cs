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

            ViewBag.MentorshipData = (completedAsMentor, completedAsMentee);
            ViewBag.Projects = projects;
            ViewBag.Biddings = biddings;

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
