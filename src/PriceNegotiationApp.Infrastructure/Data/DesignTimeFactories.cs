using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PriceNegotiationApp.Infrastructure.Persistence;

namespace PriceNegotiationApp.Infrastructure.Data;

public sealed class IdentityDesignTimeFactory : IDesignTimeDbContextFactory<IdentityModuleDbContext>
{
    public IdentityModuleDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<IdentityModuleDbContext>()
            .UseNpgsql(DesignTime.ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Identity"))
            .UseSnakeCaseNamingConvention()
            .Options);
}

public sealed class CatalogDesignTimeFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(DesignTime.ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Catalog"))
            .UseSnakeCaseNamingConvention()
            .Options);
}

public sealed class NegotiationsDesignTimeFactory : IDesignTimeDbContextFactory<NegotiationsDbContext>
{
    public NegotiationsDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<NegotiationsDbContext>()
            .UseNpgsql(DesignTime.ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Negotiations"))
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
