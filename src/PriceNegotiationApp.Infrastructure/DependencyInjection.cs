using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PriceNegotiationApp.Application.Abstractions;
using PriceNegotiationApp.Infrastructure.Persistence;
using PriceNegotiationApp.Infrastructure.Persistence.Repositories;

namespace PriceNegotiationApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration["Database:ConnectionString"])
                .UseSnakeCaseNamingConvention());

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<INegotiationRepository, NegotiationRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();

        return services;
    }
}
