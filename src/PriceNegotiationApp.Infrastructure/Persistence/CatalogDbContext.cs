using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Domain.Models;
using PriceNegotiationApp.Infrastructure.Persistence.DbEntityConfigurations;

namespace PriceNegotiationApp.Infrastructure.Persistence;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("catalog");
        // Explicit registration: configurations are owned per context, never assembly-scanned.
        modelBuilder.ApplyConfiguration(new ProductConfiguration());
    }
}
