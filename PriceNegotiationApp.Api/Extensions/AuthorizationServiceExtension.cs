using Microsoft.AspNetCore.Authorization;

namespace PriceNegotiationApp.Api.Extensions
{
    public static class AuthorizationServiceExtension
    {
        internal static void AddAuthorizationWithPolicies(this IServiceCollection services)
        {
            services.AddAuthorization();
            services.AddSingleton<IAuthorizationHandler, NegotiationAuthorizationHandler>();
        }
    }
}
