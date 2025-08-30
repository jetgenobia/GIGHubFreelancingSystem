using Microsoft.AspNetCore.Identity;

namespace Freelancing.Services
{
    public interface IRoleSeederService
    {
        Task SeedRolesAsync();
    }
}
