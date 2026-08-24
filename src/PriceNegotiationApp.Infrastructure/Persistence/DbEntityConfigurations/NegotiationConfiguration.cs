using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceNegotiationApp.Domain.Models;
using PriceNegotiationApp.Domain.ValueObjects.Ids;

namespace PriceNegotiationApp.Infrastructure.Persistence.DbEntityConfigurations;

public sealed class NegotiationConfiguration : IEntityTypeConfiguration<Negotiation>
{
    public void Configure(EntityTypeBuilder<Negotiation> builder)
    {
        builder.ToTable("negotiations");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasConversion(id => id.Value, value => NegotiationId.From(value))
            .ValueGeneratedNever();
        builder.Property(n => n.ProductId).HasConversion(id => id.Value, value => ProductId.From(value));
        builder.Property(n => n.CustomerId).HasConversion(id => id.Value, value => CustomerId.From(value));
        builder.Property(n => n.BasePrice).HasColumnType("numeric(18,2)");
        builder.Property(n => n.CurrentOffer).HasColumnType("numeric(18,2)");
        builder.Property(n => n.Status).HasConversion<int>();
        // No FK to catalog.products by design (separate schemas/modules). Product existence is
        // validated at negotiation creation; negotiations survive product deletion on snapshots.
        builder.HasOne<Customer>().WithMany().HasForeignKey(n => n.CustomerId).OnDelete(DeleteBehavior.Cascade);
        // One OPEN negotiation per customer per product; closed history preserved.
        builder.HasIndex(n => new { n.ProductId, n.CustomerId })
            .IsUnique()
            .HasFilter($"status = {(int)NegotiationStatus.Open}");
        builder.Property(n => n.Version).IsRowVersion();
    }
}
