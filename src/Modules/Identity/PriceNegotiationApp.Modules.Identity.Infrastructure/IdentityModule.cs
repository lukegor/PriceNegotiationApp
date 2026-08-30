using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PriceNegotiationApp.Modules.Identity.Contracts;
using PriceNegotiationApp.Modules.Identity.Application.Login;
using PriceNegotiationApp.Modules.Identity.Infrastructure.Login;
using PriceNegotiationApp.Modules.Identity.Infrastructure.Register;
using PriceNegotiationApp.Modules.Identity.Infrastructure.Persistence;
using PriceNegotiationApp.Modules.Identity.Infrastructure.Seeding;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Identity.Infrastructure;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdentityModuleDbContext>(options => options
            .UseNpgsql(DbConnections.Resolve(configuration, "Identity"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Identity"))
            .UseSnakeCaseNamingConvention());

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<IdentityModuleDbContext>()
            .AddDefaultTokenProviders();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services.AddSingleton(TimeProvider.System);
        services.AddValidatorsFromAssemblyContaining<LoginRequest>();
        services.AddSingleton<EcSigningKey>();
        services.AddSingleton<JwtManager>();
        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<LoginUserHandler>();

        services.AddOptions<SeedingOptions>()
            .Bind(configuration.GetSection(SeedingOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<SeedingOptions>, SeedingOptionsValidator>();
        services.AddHostedService<IdentitySeedingHostedService>();

        return services;
    }
}
