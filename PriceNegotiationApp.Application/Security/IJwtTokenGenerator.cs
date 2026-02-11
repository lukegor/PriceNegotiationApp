using System.Security.Claims;

namespace PriceNegotiationApp.Application.Security
{
    public interface IJwtTokenGenerator
    {
        Task<string> GenerateToken(IReadOnlyCollection<Claim> claims);
    }
}
