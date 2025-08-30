using Microsoft.AspNetCore.Identity;

namespace Freelancing.Services
{
    public class RoleSeederService : IRoleSeederService
    {
        private readonly RoleManager<IdentityRole> _roleManager;

        public RoleSeederService(RoleManager<IdentityRole> roleManager)
        {
            _roleManager = roleManager;
        }

        public async Task SeedRolesAsync()
        {
            string[] roles = { "Freelancer", "Client", "Mentor", "Mentee" };

            foreach (string role in roles)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }
    }
}
