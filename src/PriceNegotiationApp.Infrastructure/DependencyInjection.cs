using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PriceNegotiationApp.Application.Abstractions;
using PriceNegotiationApp.Infrastructure.Auth;
using PriceNegotiationApp.Infrastructure.Identity;
using PriceNegotiationApp.Infrastructure.Persistence;
using PriceNegotiationApp.Infrastructure.Persistence.Repositories;
using PriceNegotiationApp.Infrastructure.Seeding;

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

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();
        services.AddScoped<IUserAccountStore, IdentityAccountStore>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services.AddSingleton<IJwtTokenGenerator, JwtManager>();

        services.AddOptions<SeedingOptions>()
            .Bind(configuration.GetSection(SeedingOptions.SectionName));

        services.AddHostedService<SeedingHostedService>();

        return services;
    }
}
