using Microsoft.Extensions.DependencyInjection;
using PriceNegotiationApp.Application.Features.Auth;

namespace PriceNegotiationApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}



