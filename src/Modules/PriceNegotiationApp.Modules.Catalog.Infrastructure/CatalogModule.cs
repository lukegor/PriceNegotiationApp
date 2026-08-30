using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PriceNegotiationApp.Modules.Catalog.Application.Create;
using PriceNegotiationApp.Modules.Catalog.Application.Update;
using PriceNegotiationApp.Modules.Catalog.Infrastructure.Create;
using PriceNegotiationApp.Modules.Catalog.Infrastructure.Delete;
using PriceNegotiationApp.Modules.Catalog.Infrastructure.Get;
using PriceNegotiationApp.Modules.Catalog.Infrastructure.List;
using PriceNegotiationApp.Modules.Catalog.Infrastructure.Update;
using PriceNegotiationApp.Modules.Catalog.Infrastructure.Persistence;
using PriceNegotiationApp.Modules.Catalog.Infrastructure.Seeding;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Catalog.Infrastructure;

public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CatalogDbContext>(options => options
            .UseNpgsql(DbConnections.Resolve(configuration, "Catalog"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Catalog"))
            .UseSnakeCaseNamingConvention());
        // Deliberately unvalidated: CatalogSeedingOptions is a single optional bool
        // with no meaningful validation surface (engineering-hardening spec §7).
        services.AddOptions<CatalogSeedingOptions>()
            .Bind(configuration.GetSection(CatalogSeedingOptions.SectionName));
        services.AddHostedService<CatalogSeedingHostedService>();
        services.AddValidatorsFromAssemblyContaining<CreateProductRequest>();
        services.AddScoped<CreateProductHandler>();
        services.AddScoped<UpdateProductHandler>();
        services.AddScoped<DeleteProductHandler>();
        services.AddScoped<GetProductHandler>();
        services.AddScoped<ListProductsHandler>();
        return services;
    }
}
