using Microsoft.Extensions.Configuration;

namespace PriceNegotiationApp.BuildingBlocks;

public static class DbConnections
{
    private const string DefaultKey = "Database:ConnectionString";

    /// <summary>Per-module override wins; falls back to the shared connection string.</summary>
    public static string Resolve(IConfiguration configuration, string moduleName)
    {
        var moduleOverride = configuration[$"Database:Modules:{moduleName}:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(moduleOverride))
        {
            return moduleOverride;
        }

        return configuration[DefaultKey]
               ?? throw new InvalidOperationException(
                   $"{DefaultKey} is not configured (module '{moduleName}').");
    }
}
