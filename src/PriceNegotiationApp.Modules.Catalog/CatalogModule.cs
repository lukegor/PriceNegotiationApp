using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PriceNegotiationApp.SharedKernel;
using PriceNegotiationApp.Modules.Catalog.Persistence;
using PriceNegotiationApp.Modules.Catalog.Seeding;

namespace PriceNegotiationApp.Modules.Catalog;

public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CatalogDbContext>(options => options
            .UseNpgsql(DbConnections.Resolve(configuration, "Catalog"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Catalog"))
            .UseSnakeCaseNamingConvention());
        services.AddOptions<CatalogSeedingOptions>()
            .Bind(configuration.GetSection(CatalogSeedingOptions.SectionName));
        services.AddHostedService<CatalogSeedingHostedService>();
        return services;
    }
}
