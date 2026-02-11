using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Domain.Models.Negotiations;
using PriceNegotiationApp.Domain.Models.Products;

namespace PriceNegotiationApp.Application.Common
{
    public interface IAppDbContext
    {
        DbSet<Product> Products { get; }
        DbSet<Negotiation> Negotiations { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        int SaveChanges();
    }
}
