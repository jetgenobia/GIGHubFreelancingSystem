using Freelancing.Data;
using Freelancing.Models;
using Freelancing.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Freelancing.Controllers
{
    public class SearchController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SearchController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View(new SearchViewModel());
        }

        [HttpGet]
        public async Task<IActionResult> Search(string searchTerm, string searchType = "all")
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return RedirectToAction(nameof(Index));
            }

            var viewModel = new SearchViewModel
            {
                SearchTerm = searchTerm,
                SearchType = searchType
            };

            searchTerm = searchTerm.ToLower();

            // Search for users
            if (searchType == "all" || searchType == "users")
            {
                viewModel.Users = await _context.UserAccounts
                    .Include(u => u.UserAccountSkills)
                        .ThenInclude(us => us.UserSkill)
                    .Where(u => u.FirstName.ToLower().Contains(searchTerm) ||
                               u.LastName.ToLower().Contains(searchTerm) ||
                               u.UserName.ToLower().Contains(searchTerm) ||
                               (u.Bio != null && u.Bio.ToLower().Contains(searchTerm)) ||
                               u.UserAccountSkills.Any(us => us.UserSkill.Name.ToLower().Contains(searchTerm)))
                    .Take(20)
                    .ToListAsync();
            }

            // Search for projects
            if (searchType == "all" || searchType == "projects")
            {
                viewModel.Projects = await _context.Projects
                    .Include(p => p.User)
                    .Include(p => p.ProjectSkills)
                        .ThenInclude(ps => ps.UserSkill)
                    .Where(p => p.ProjectName.ToLower().Contains(searchTerm) ||
                               p.ProjectDescription.ToLower().Contains(searchTerm) ||
                               p.Category.ToLower().Contains(searchTerm) ||
                               p.ProjectSkills.Any(ps => ps.UserSkill.Name.ToLower().Contains(searchTerm)))
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(20)
                    .ToListAsync();
            }

            return View("Index", viewModel);
        }
    }
}
