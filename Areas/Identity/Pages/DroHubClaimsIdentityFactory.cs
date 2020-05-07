using System.Security.Claims;
using System.Threading.Tasks;
using DroHub.Areas.Identity.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace DroHub.Areas.Identity.Pages {
    public class DroHubClaimsIdentityFactory : UserClaimsPrincipalFactory<DroHubUser>
    {
        public DroHubClaimsIdentityFactory(UserManager<DroHubUser> userManager,
            IOptions<IdentityOptions> optionsAccessor):base(userManager, optionsAccessor)
        {}

        public override async Task<ClaimsPrincipal> CreateAsync(DroHubUser user) {
            var principal = await base.CreateAsync(user);
            ((ClaimsIdentity) principal.Identity).AddClaims(new[] {
                new Claim(DroHubUser.BASE_ACTING_TYPE_KEY_CLAIM, user.BaseActingType)
            });
            return principal;
        }
    }
}