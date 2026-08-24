using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Negotiations.Persistence;

public sealed class DesignTimeDbContextFactory : DesignTimeDbContextFactoryBase<NegotiationsDbContext>
{
    protected override void Configure(DbContextOptionsBuilder<NegotiationsDbContext> builder) =>
        builder.UseNpgsql(LocalConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Negotiations"))
            .UseSnakeCaseNamingConvention();

    protected override NegotiationsDbContext Create(DbContextOptions<NegotiationsDbContext> options) => new(options);
}
