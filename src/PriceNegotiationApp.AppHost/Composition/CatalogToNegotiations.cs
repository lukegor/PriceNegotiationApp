using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Modules.Catalog.Domain;
using PriceNegotiationApp.Modules.Catalog.Persistence;
using PriceNegotiationApp.Modules.Negotiations.Ports;

namespace PriceNegotiationApp.AppHost.Composition;

/// <summary>The single sanctioned inter-module edge: Negotiations reads product price snapshots.</summary>
public sealed class CatalogToNegotiations(CatalogDbContext db) : IProductPriceProvider
{
    public async Task<ProductSnapshot?> GetAsync(Guid productId, CancellationToken ct) =>
        await db.Products.AsNoTracking()
            .Where(p => p.Id == ProductId.From(productId))
            .Select(p => new ProductSnapshot(productId, p.Price))
            .FirstOrDefaultAsync(ct);
}




