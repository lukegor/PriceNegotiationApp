using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceNegotiationApp.Modules.Negotiations.Domain;

namespace PriceNegotiationApp.Modules.Negotiations.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasConversion(id => id.Value, value => CustomerId.From(value))
            .ValueGeneratedNever();
        builder.HasIndex(c => c.IdentityUserId).IsUnique();
    }
}
