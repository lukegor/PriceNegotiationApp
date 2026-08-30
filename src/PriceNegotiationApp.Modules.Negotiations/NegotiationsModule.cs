using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.Accept;
using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.CounterPropose;
using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.Create;
using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.Get;
using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.List;
using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.ListMine;
using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.RejectCurrentOffer;
using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.Withdraw;
using PriceNegotiationApp.Modules.Negotiations.Persistence;
using PriceNegotiationApp.SharedKernel;

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
        services.AddScoped<CreateNegotiationHandler>();
        services.AddScoped<CounterProposeHandler>();
        services.AddScoped<AcceptHandler>();
        services.AddScoped<RejectCurrentOfferHandler>();
        services.AddScoped<WithdrawHandler>();
        services.AddScoped<GetNegotiationHandler>();
        services.AddScoped<ListNegotiationsHandler>();
        services.AddScoped<ListMyNegotiationsHandler>();
        return services;
    }
}
