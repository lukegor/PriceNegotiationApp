using PriceNegotiationApp.Application.Common;
using System.Security.Claims;

namespace PriceNegotiationApp.Api.Providers
{
    public class HttpExecutionContext : IExecutionContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HttpExecutionContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid? UserId => Guid.Parse(_httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier));
        public string? Role => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role);

        public ClaimsPrincipal User => _httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
    }
}
