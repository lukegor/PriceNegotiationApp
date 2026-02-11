using System.Security.Claims;

namespace PriceNegotiationApp.Application.Common
{
    public interface IExecutionContext
    {
        Guid? UserId { get; }
        string Role { get; }
        ClaimsPrincipal User { get; }
    }
}
