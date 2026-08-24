using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace PriceNegotiationApp.SharedKernel;

/// <summary>
/// Runs a module's seed routine once at host start inside a scope that is disposed afterwards.
/// </summary>
public abstract class ModuleSeedingHostedServiceBase(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await SeedAsync(scope.ServiceProvider, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Seed the module's data. Resolve services from <paramref name="services"/>.</summary>
    protected abstract Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken);
}
