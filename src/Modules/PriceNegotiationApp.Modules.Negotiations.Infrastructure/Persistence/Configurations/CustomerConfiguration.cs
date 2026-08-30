using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceNegotiationApp.Modules.Negotiations.Domain;

namespace PriceNegotiationApp.Modules.Negotiations.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        // DELIBERATE ANEMIC DESIGN (ddd-audit spec F-03): Customer is a reference row
        // binding an ASP.NET Identity user into this context. It is created once and
        // never mutated; it has no behavioral invariants beyond a non-empty identity
        // link. Do not "enrich" it into a fake aggregate without a real use case.
        builder.ToTable("customers");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasConversion(id => id.Value, value => CustomerId.From(value))
            .ValueGeneratedNever();
        builder.HasIndex(c => c.IdentityUserId).IsUnique();
    }
}
