using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceNegotiationApp.Modules.Negotiations.Domain;

namespace PriceNegotiationApp.Modules.Negotiations.Persistence.Configurations;

internal sealed class NegotiationConfiguration : IEntityTypeConfiguration<Negotiation>
{
    public void Configure(EntityTypeBuilder<Negotiation> builder)
    {
        builder.ToTable("negotiations");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasConversion(id => id.Value, value => NegotiationId.From(value))
            .ValueGeneratedNever();
        // Plain Guid key: product_id has NO FK by design (separate schemas/modules).
        // Existence is validated at creation; negotiations survive deletion on snapshots.
        builder.Property(n => n.CustomerId).HasConversion(id => id.Value, value => CustomerId.From(value));
        builder.Property(n => n.BasePrice).HasColumnType("numeric(18,2)");
        builder.Property(n => n.CurrentOffer).HasColumnType("numeric(18,2)");
        builder.Property(n => n.Status).HasConversion<int>();
        builder.HasOne<Customer>().WithMany().HasForeignKey(n => n.CustomerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(n => new { n.ProductId, n.CustomerId })
            .IsUnique()
            .HasFilter($"status = {(int)NegotiationStatus.Open}");
        builder.Property(n => n.Version).IsRowVersion();
    }
}
