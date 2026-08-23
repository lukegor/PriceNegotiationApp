using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceNegotiationApp.Domain.Models.Products;

namespace PriceNegotiationApp.Infrastructure.DbEntityConfigurations
{
    internal class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.ToTable("Products");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id);
            builder.Property(p => p.Name).IsRequired().HasMaxLength(200);

            builder.OwnsOne(p =>
                p.Price,
                ownsBuilder =>
                {
                    ownsBuilder
                        .Property(p => p.Value)
                        .IsRequired();
                });
        }
    }
}
