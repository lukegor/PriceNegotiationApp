using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.Modules.Negotiations.Application.Create;
using PriceNegotiationApp.Modules.Negotiations.Infrastructure.Accept;
using PriceNegotiationApp.Modules.Negotiations.Infrastructure.CounterPropose;
using PriceNegotiationApp.Modules.Negotiations.Infrastructure.Create;
using PriceNegotiationApp.Modules.Negotiations.Infrastructure.Get;
using PriceNegotiationApp.Modules.Negotiations.Infrastructure.List;
using PriceNegotiationApp.Modules.Negotiations.Infrastructure.ListMine;
using PriceNegotiationApp.Modules.Negotiations.Infrastructure.RejectCurrentOffer;
using PriceNegotiationApp.Modules.Negotiations.Infrastructure.Withdraw;
using PriceNegotiationApp.Modules.Negotiations.Infrastructure.Persistence;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Negotiations.Infrastructure;

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
        services.AddValidatorsFromAssemblyContaining<CreateNegotiationRequest>();
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
