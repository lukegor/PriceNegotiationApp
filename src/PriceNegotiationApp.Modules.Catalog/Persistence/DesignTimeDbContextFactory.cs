using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Catalog.Persistence;

public sealed class DesignTimeDbContextFactory : DesignTimeDbContextFactoryBase<CatalogDbContext>
{
    protected override void Configure(DbContextOptionsBuilder<CatalogDbContext> builder) =>
        builder.UseNpgsql(LocalConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Catalog"))
            .UseSnakeCaseNamingConvention();

    protected override CatalogDbContext Create(DbContextOptions<CatalogDbContext> options) => new(options);
}
