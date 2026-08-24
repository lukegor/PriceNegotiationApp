using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PriceNegotiationApp.Infrastructure.Persistence;
using PriceNegotiationApp.Modules.Negotiations.Persistence;

namespace PriceNegotiationApp.Api.Composition;

public sealed class MigrationHostedService(IServiceScopeFactory scopeFactory, ILogger<MigrationHostedService> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await MigrateAsync<IdentityModuleDbContext>(scope, cancellationToken);
        await MigrateAsync<CatalogDbContext>(scope, cancellationToken);
        await MigrateAsync<NegotiationsDbContext>(scope, cancellationToken);
        logger.LogInformation("Module databases migrated.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task MigrateAsync<T>(IServiceScope scope, CancellationToken ct) where T : DbContext
    {
        var db = scope.ServiceProvider.GetRequiredService<T>();
        await db.Database.MigrateAsync(ct);
    }
}

