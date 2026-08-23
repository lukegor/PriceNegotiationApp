using Microsoft.Extensions.DependencyInjection;
using PriceNegotiationApp.Application.Features.Auth;
using PriceNegotiationApp.Application.Features.Negotiations;
using PriceNegotiationApp.Application.Features.Products;
using PriceNegotiationApp.Domain.Policy;

namespace PriceNegotiationApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<INegotiationPolicy, DefaultNegotiationPolicy>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<INegotiationService, NegotiationService>();
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
