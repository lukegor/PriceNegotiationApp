using System.Security.Claims;

namespace PriceNegotiationApp.Services.Providers
{
    public interface IClaimsProvider
    {
        public ClaimsPrincipal UserClaimsPrincipal { get; }
    }
}
