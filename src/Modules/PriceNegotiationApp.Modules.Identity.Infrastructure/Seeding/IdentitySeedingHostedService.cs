using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceNegotiationApp.Modules.Identity.Infrastructure.Persistence;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Identity.Infrastructure.Seeding;

internal sealed class IdentitySeedingHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<SeedingOptions> options,
    ILogger<IdentitySeedingHostedService> logger) : ModuleSeedingHostedServiceBase(scopeFactory)
{
    protected override async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { UserRoles.Admin, UserRoles.Staff, UserRoles.Customer })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        await EnsureUserAsync(userManager, options.Value.AdminEmail, options.Value.AdminPassword, UserRoles.Admin);
        await EnsureUserAsync(userManager, options.Value.StaffEmail, options.Value.StaffPassword, UserRoles.Staff);
        logger.LogInformation("Identity seed data ensured.");
    }

    private async Task EnsureUserAsync(
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
        else
        {
            logger.LogError("Seeded user {Email} could not be created: {Errors}",
                email, string.Join("; ", result.Errors.Select(e => $"{e.Code} {e.Description}")));
        }
    }
}
