using System.Security.Claims;

namespace PriceNegotiationApp.Services.Providers
{
    public class HttpExecutionContext : IExecutionContext
    {
        public string UserId { get; }
        public string Role { get; }

        public HttpExecutionContext(IHttpContextAccessor httpContextAccessor)
        {
            UserId = httpContextAccessor?.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            Role = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Role)?.Value;
        }
    }
}
