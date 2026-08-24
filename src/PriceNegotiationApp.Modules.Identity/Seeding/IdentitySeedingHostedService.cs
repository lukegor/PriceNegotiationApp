using PriceNegotiationApp.BuildingBlocks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceNegotiationApp.Modules.Identity.Persistence;

namespace PriceNegotiationApp.Modules.Identity.Seeding;

public sealed class IdentitySeedingHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<SeedingOptions> options,
    ILogger<IdentitySeedingHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { UserRoles.Admin, UserRoles.Staff, UserRoles.Customer })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await EnsureUserAsync(userManager, options.Value.AdminEmail, options.Value.AdminPassword, UserRoles.Admin);
        await EnsureUserAsync(userManager, options.Value.StaffEmail, options.Value.StaffPassword, UserRoles.Staff);
        logger.LogInformation("Identity seed data ensured.");
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


