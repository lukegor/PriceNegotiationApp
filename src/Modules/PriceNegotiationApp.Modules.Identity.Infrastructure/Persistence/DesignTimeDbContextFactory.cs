using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.SharedKernel;

using System.Diagnostics.CodeAnalysis;

namespace PriceNegotiationApp.Modules.Identity.Infrastructure.Persistence;

[SuppressMessage("Meziantou.Analyzer", "MA0182", Justification = "Instantiated by EF Core design-time tooling via reflection.")]
internal sealed class DesignTimeDbContextFactory : DesignTimeDbContextFactoryBase<IdentityModuleDbContext>
{
    protected override void Configure(DbContextOptionsBuilder<IdentityModuleDbContext> builder) =>
        builder.UseNpgsql(LocalConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Identity"))
            .UseSnakeCaseNamingConvention();

    protected override IdentityModuleDbContext Create(DbContextOptions<IdentityModuleDbContext> options) => new(options);
}
