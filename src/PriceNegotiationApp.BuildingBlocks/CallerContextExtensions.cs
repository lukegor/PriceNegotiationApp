using System.Security.Claims;

namespace PriceNegotiationApp.BuildingBlocks;

public static class CallerContextExtensions
{
    public static CallerContext ToCallerContext(this ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return CallerContext.Anonymous;
        }

        _ = Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId);
        var email = principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet();
        return new CallerContext(userId, email, roles);
    }
}
