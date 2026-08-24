using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.SharedKernel;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.Modules.Negotiations.Persistence.Configurations;

namespace PriceNegotiationApp.Modules.Negotiations.Persistence;

internal sealed class NegotiationsDbContext(DbContextOptions<NegotiationsDbContext> options) : DbContext(options)
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


