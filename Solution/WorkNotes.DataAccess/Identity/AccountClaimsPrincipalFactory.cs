using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using WorkNotes.DataAccess.Entities;

namespace WorkNotes.DataAccess.Identity;

public sealed class AccountClaimsPrincipalFactory(UserManager<ApplicationUser> manager, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser>(manager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(ClaimTypes.GivenName, user.FirstName));
        identity.AddClaim(new Claim(ClaimTypes.Surname, user.LastName));
        return identity;
    }
}

