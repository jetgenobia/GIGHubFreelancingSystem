using Freelancing.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Freelancing.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class DeleteUserModel : PageModel
    {
        private readonly UserCleanupHostedService _cleanupService;

        public DeleteUserModel(UserCleanupHostedService cleanupService)
        {
            _cleanupService = cleanupService;
        }

        public async Task<IActionResult> OnPostAsync(string userId)
        {
            await _cleanupService.DeleteUserImmediately(userId);
            TempData["Message"] = "User deleted immediately.";
            return RedirectToPage("/Admin/Users");
        }
    }
}