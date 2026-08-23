using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Domain.Models;
using PriceNegotiationApp.Domain.ValueObjects;
using PriceNegotiationApp.Infrastructure.Identity;
using PriceNegotiationApp.Infrastructure.Persistence;

namespace PriceNegotiationApp.Infrastructure.Seeding;

public sealed class SeedingHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<SeedingOptions> seedingOptions,
    ILogger<SeedingHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(cancellationToken);

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { UserRoles.Admin, UserRoles.Staff, UserRoles.Customer })
        {
            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var options = seedingOptions.Value;
        await EnsureUserAsync(userManager, options.AdminEmail, options.AdminPassword, UserRoles.Admin);
        await EnsureUserAsync(userManager, options.StaffEmail, options.StaffPassword, UserRoles.Staff);

        if (options.SeedSampleProducts && !await db.Products.AnyAsync(cancellationToken))
        {
            db.Products.AddRange(
                Product.Create("Mechanical Keyboard", Price.From(249.00m)),
                Product.Create("Wireless Mouse", Price.From(79.90m)),
                Product.Create("USB-C Docking Station", Price.From(189.50m)));
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Database migrated and seed data ensured.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager, string email, string password, string role)
    {
        if (string.IsNullOrWhiteSpace(password)
            || await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var user = new ApplicationUser { UserName = email, Email = email };
        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }
}

