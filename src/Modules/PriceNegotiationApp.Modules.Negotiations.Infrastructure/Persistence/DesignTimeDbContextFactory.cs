using PriceNegotiationApp.Modules.Negotiations.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.SharedKernel;

using System.Diagnostics.CodeAnalysis;

namespace PriceNegotiationApp.Modules.Negotiations.Infrastructure.Persistence;

[SuppressMessage("Meziantou.Analyzer", "MA0182", Justification = "Instantiated by EF Core design-time tooling via reflection.")]
internal sealed class DesignTimeDbContextFactory : DesignTimeDbContextFactoryBase<NegotiationsDbContext>
{
    protected override void Configure(DbContextOptionsBuilder<NegotiationsDbContext> builder) =>
        builder.UseNpgsql(LocalConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Negotiations"))
            .UseSnakeCaseNamingConvention();

    protected override NegotiationsDbContext Create(DbContextOptions<NegotiationsDbContext> options) => new(options);
}
