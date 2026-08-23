using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using PriceNegotiationApp.Domain.Models.Customers;
using PriceNegotiationApp.Domain.Models.Negotiations;

namespace PriceNegotiationApp.Infrastructure.DbEntityConfigurations
{
    internal class CustomerConfiguration : IEntityTypeConfiguration<Customer>
    {
        public void Configure(EntityTypeBuilder<Customer> builder)
        {
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id);

            builder.HasMany<Negotiation>()
                .WithOne()
                .HasForeignKey(n => n.UserId);

            builder.Property(c => c.IdentityId)
                .IsRequired();
            builder.HasIndex(c => c.IdentityId)
                .IsUnique();

            builder.Property(c => c.Name)
                .IsRequired();
        }
    }
}
