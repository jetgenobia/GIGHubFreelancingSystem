using Freelancing.Data;
using Freelancing.Models.Entities;
using Freelancing.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace Freelancing.Controllers
{
    [Authorize]
    public class SkillController : Controller
    {
        private readonly ApplicationDbContext dbContext;

        public SkillController(ApplicationDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> EditSkills(string searchTerm, List<Guid> selectedSkillIds)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Get ALL skills for skill name lookups
            var allSkills = await dbContext.UserSkills.ToListAsync();

            // Get filtered skills for display based on search term
            var filteredSkills = allSkills
                .Where(s => string.IsNullOrEmpty(searchTerm) || s.Name.Contains(searchTerm))
                .OrderBy(s => s.Name)
                .ToList();

            // If selectedSkillIds are passed (from search), use those; otherwise get from database
            List<Guid> currentSelectedSkillIds;
            if (selectedSkillIds != null && selectedSkillIds.Any())
            {
                currentSelectedSkillIds = selectedSkillIds;
            }
            else
            {
                // Get currently selected skills for the user from database
                currentSelectedSkillIds = await dbContext.UserAccountSkills
                    .Where(uas => uas.UserAccountId == userId)
                    .Select(uas => uas.UserSkillId)
                    .ToListAsync();
            }

            var viewModel = new EditSkills
            {
                UserSkills = filteredSkills, // Only filtered skills for the available skills section
                AllUserSkills = allSkills,   // All skills for skill name lookups
                SelectedSkillIds = currentSelectedSkillIds,
                SearchTerm = searchTerm ?? string.Empty
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> EditSkills(EditSkills viewModel)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            try
            {
                // Remove existing skills for the user
                var existingUserSkills = await dbContext.UserAccountSkills
                    .Where(uas => uas.UserAccountId == userId)
                    .ToListAsync();

                dbContext.UserAccountSkills.RemoveRange(existingUserSkills);

                // Add selected skills
                if (viewModel.SelectedSkillIds != null && viewModel.SelectedSkillIds.Any())
                {
                    var newUserSkills = viewModel.SelectedSkillIds.Select(skillId => new UserAccountSkill
                    {
                        UserAccountId = userId,
                        UserSkillId = skillId
                    }).ToList();

                    await dbContext.UserAccountSkills.AddRangeAsync(newUserSkills);
                }

                await dbContext.SaveChangesAsync();
                TempData["SuccessMessage"] = "Skills updated successfully!";
            }
            catch (Exception ex)
            {
                // Log the error
                TempData["ErrorMessage"] = "An error occurred while updating skills.";
                // You might want to log the exception here
            }

            return RedirectToAction("EditSkills");
        }
    }
}