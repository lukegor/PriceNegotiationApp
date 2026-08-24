using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceNegotiationApp.Modules.Catalog.Domain;
using PriceNegotiationApp.Modules.Catalog.Persistence;

namespace PriceNegotiationApp.Modules.Catalog.Seeding;

public sealed class CatalogSeedingHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<CatalogSeedingOptions> options,
    ILogger<CatalogSeedingHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.SeedSampleProducts)
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        if (!await db.Products.AnyAsync(cancellationToken))
        {
            db.Products.AddRange(
                Product.Create("Mechanical Keyboard", 249.00m),
                Product.Create("Wireless Mouse", 79.90m),
                Product.Create("USB-C Docking Station", 189.50m));
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Catalog seed data ensured.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
