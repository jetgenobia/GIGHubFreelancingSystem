using Freelancing.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Freelancing.Services
{
    public class CustomUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<UserAccount, IdentityRole>
    {
        public CustomUserClaimsPrincipalFactory(
            UserManager<UserAccount> userManager,
            RoleManager<IdentityRole> roleManager,
            IOptions<IdentityOptions> optionsAccessor)
            : base(userManager, roleManager, optionsAccessor)
        {
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(UserAccount user)
        {
            var identity = await base.GenerateClaimsAsync(user);

            // Add custom claims
            var fullName = $"{user.FirstName ?? string.Empty} {user.LastName ?? string.Empty}".Trim();
            identity.AddClaim(new Claim("FullName", fullName));
            identity.AddClaim(new Claim("Photo", user.Photo ?? string.Empty));

            identity.AddClaim(new Claim(ClaimTypes.GivenName, user.FirstName ?? string.Empty));
            identity.AddClaim(new Claim(ClaimTypes.Surname, user.LastName ?? string.Empty));

            if (user.MentorshipId.HasValue)
            {
                identity.AddClaim(new Claim("MentorshipId", user.MentorshipId.Value.ToString()));
            }

            return identity;
        }
    }
}
