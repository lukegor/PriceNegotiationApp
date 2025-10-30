using System.Security.Claims;

namespace PriceNegotiationApp.Services.Providers
{
    public interface IExecutionContext
    {
        string UserId { get; }
        string Role { get; }
    }
}
