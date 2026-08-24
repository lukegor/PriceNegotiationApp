using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PriceNegotiationApp.SharedKernel;

/// <summary>
/// Common plumbing for EF Core design-time factories. Provider configuration stays in each
/// module on purpose: Npgsql must not become a SharedKernel dependency.
/// </summary>
public abstract class DesignTimeDbContextFactoryBase<TContext> : IDesignTimeDbContextFactory<TContext>
    where TContext : DbContext
{
#pragma warning disable S2068 // Design-time default only; never used in production wiring.
    protected const string LocalConnectionString =
        "Host=localhost;Port=5432;Database=pricenego_design;Username=postgres;Password=postgres";
#pragma warning restore S2068

    public TContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        Configure(builder);
        return Create(builder.Options);
    }

    /// <summary>Apply provider options, e.g. UseNpgsql(LocalConnectionString, ...) plus naming conventions.</summary>
    protected abstract void Configure(DbContextOptionsBuilder<TContext> builder);

    /// <summary>Create the context instance, typically `new TContext(options)`.</summary>
    protected abstract TContext Create(DbContextOptions<TContext> options);
}
