using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using Freelancing.Models.Entities;

namespace Freelancing.Services
{
    // Custom Claims Factory to ensure FirstName, LastName, and FullName claims are always available
    public class CustomClaimsFactory : UserClaimsPrincipalFactory<UserAccount, IdentityRole>
    {
        public CustomClaimsFactory(UserManager<UserAccount> userManager, RoleManager<IdentityRole> roleManager, IOptions<IdentityOptions> optionsAccessor)
            : base(userManager, roleManager, optionsAccessor)
        {
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(UserAccount user)
        {
            var identity = await base.GenerateClaimsAsync(user);
            
            // Add custom claims
            if (!string.IsNullOrEmpty(user.FirstName))
                identity.AddClaim(new Claim(ClaimTypes.GivenName, user.FirstName));
                
            if (!string.IsNullOrEmpty(user.LastName))
                identity.AddClaim(new Claim(ClaimTypes.Surname, user.LastName));
                
            if (!string.IsNullOrEmpty(user.FirstName) || !string.IsNullOrEmpty(user.LastName))
            {
                var fullName = $"{user.FirstName ?? string.Empty} {user.LastName ?? string.Empty}".Trim();
                if (!string.IsNullOrEmpty(fullName))
                    identity.AddClaim(new Claim("FullName", fullName));
            }
            
            return identity;
        }
    }
}
