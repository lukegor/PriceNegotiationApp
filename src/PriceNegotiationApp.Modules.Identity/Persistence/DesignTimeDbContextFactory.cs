using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Identity.Persistence;

public sealed class DesignTimeDbContextFactory : DesignTimeDbContextFactoryBase<IdentityModuleDbContext>
{
    protected override void Configure(DbContextOptionsBuilder<IdentityModuleDbContext> builder) =>
        builder.UseNpgsql(LocalConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Identity"))
            .UseSnakeCaseNamingConvention();

    protected override IdentityModuleDbContext Create(DbContextOptions<IdentityModuleDbContext> options) => new(options);
}
