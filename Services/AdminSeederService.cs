using Freelancing.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace Freelancing.Services
{
    public class AdminSeederService
    {
        private readonly UserManager<UserAccount> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;

        public AdminSeederService(
            UserManager<UserAccount> userManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _configuration = configuration;
        }

        public async Task SeedAsync()
        {
            const string adminRole = "Admin";

            // Ensure Admin role exists
            if (!await _roleManager.RoleExistsAsync(adminRole))
            {
                await _roleManager.CreateAsync(new IdentityRole(adminRole));
            }

            // Read credentials from configuration (use user-secrets or env vars)
            var adminEmail = _configuration["Admin:Email"];
            var adminPassword = _configuration["Admin:Password"];

            if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
            {
                // Credentials not configured; skip seeding
                return;
            }

            // Skip if user already exists
            var existing = await _userManager.FindByEmailAsync(adminEmail);
            if (existing != null) return;

            var userName = adminEmail.Split('@')[0];

            var adminUser = new UserAccount
            {
                UserName = userName,
                Email = adminEmail,
                EmailConfirmed = true,
                FirstName = "Admin",
                LastName = "User",
                Role = adminRole,
                FRole = adminRole // satisfy required field
            };

            var createResult = await _userManager.CreateAsync(adminUser, adminPassword);
            if (!createResult.Succeeded)
            {
                // Optionally log failures; swallow to avoid failing startup
                return;
            }

            await _userManager.AddToRoleAsync(adminUser, adminRole);
        }
    }
}