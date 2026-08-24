using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PriceNegotiationApp.Modules.Identity.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<IdentityModuleDbContext>
{
    public IdentityModuleDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<IdentityModuleDbContext>()
            .UseNpgsql(DesignTime.ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Identity"))
            .UseSnakeCaseNamingConvention()
            .Options);
}

internal static class DesignTime
{
#pragma warning disable S2068 // Design-time default only; never used in production wiring.
    internal const string ConnectionString =
        "Host=localhost;Port=5432;Database=pricenego_design;Username=postgres;Password=postgres";
#pragma warning restore S2068
}
