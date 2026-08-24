using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Domain.Models;
using PriceNegotiationApp.Infrastructure.Persistence.DbEntityConfigurations;

namespace PriceNegotiationApp.Infrastructure.Persistence;

public sealed class NegotiationsDbContext(DbContextOptions<NegotiationsDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Negotiation> Negotiations => Set<Negotiation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("negotiations");
        modelBuilder.ApplyConfiguration(new CustomerConfiguration());
        modelBuilder.ApplyConfiguration(new NegotiationConfiguration());
    }
}
