using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceNegotiationApp.Domain.Models.Negotiations;
using PriceNegotiationApp.Domain.Models.Products;
using PriceNegotiationApp.Infrastructure.Identities;

namespace PriceNegotiationApp.Infrastructure.DbEntityConfigurations
{
    internal class NegotiationConfiguration : IEntityTypeConfiguration<Negotiation>
    {
        public void Configure(EntityTypeBuilder<Negotiation> builder)
        {
            builder.ToTable("Negotiations");

            builder.HasKey(n => n.Id);
            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(n => n.ProductId);
            builder.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(n => n.UserId);

            builder.Property(n => n.ProductId).IsRequired();
            builder.Property(n => n.UserId).IsRequired();
            builder.Property(n => n.RetriesLeft).IsRequired().HasPrecision(1);
            builder.Property(n => n.Status).IsRequired();
            builder.Property(n => n.CreatedAt).IsRequired();
            builder.Property(n => n.UpdatedAt).IsRequired();
            builder.Property(n => n.IsAccepted).IsRequired();
            builder.OwnsOne(n =>
                n.ProposedPrice,
                ownsBuilder =>
                {
                    ownsBuilder.Property(p => p.Value).IsRequired();
                });
        }
    }
}
