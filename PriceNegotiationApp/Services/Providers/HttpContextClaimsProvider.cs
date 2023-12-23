using System.Security.Claims;

namespace PriceNegotiationApp.Services.Providers
{
    // Adapter
    public class HttpContextClaimsProvider : IClaimsProvider
    {
        public ClaimsPrincipal UserClaimsPrincipal { get; }

        public HttpContextClaimsProvider(IHttpContextAccessor httpContextAccessor)
        {
            UserClaimsPrincipal = httpContextAccessor?.HttpContext?.User;
        }
    }
}
