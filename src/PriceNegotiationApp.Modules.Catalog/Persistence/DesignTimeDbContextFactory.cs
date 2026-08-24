using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.SharedKernel;

using System.Diagnostics.CodeAnalysis;

namespace PriceNegotiationApp.Modules.Catalog.Persistence;

[SuppressMessage("Meziantou.Analyzer", "MA0182", Justification = "Instantiated by EF Core design-time tooling via reflection.")]
internal sealed class DesignTimeDbContextFactory : DesignTimeDbContextFactoryBase<CatalogDbContext>
{
    protected override void Configure(DbContextOptionsBuilder<CatalogDbContext> builder) =>
        builder.UseNpgsql(LocalConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Catalog"))
            .UseSnakeCaseNamingConvention();

    protected override CatalogDbContext Create(DbContextOptions<CatalogDbContext> options) => new(options);
}
