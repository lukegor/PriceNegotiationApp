using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PriceNegotiationApp.SharedKernel;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.Modules.Negotiations.Persistence;

namespace PriceNegotiationApp.Modules.Negotiations;

public static class NegotiationsModule
{
    public static IServiceCollection AddNegotiationsModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<NegotiationsDbContext>(options => options
            .UseNpgsql(DbConnections.Resolve(configuration, "Negotiations"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Negotiations"))
            .UseSnakeCaseNamingConvention());
        services.AddSingleton<INegotiationPolicy, DefaultNegotiationPolicy>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
