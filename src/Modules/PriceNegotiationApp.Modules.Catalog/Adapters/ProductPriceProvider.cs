using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Modules.Catalog.Domain;
using PriceNegotiationApp.Modules.Catalog.Persistence;
using PriceNegotiationApp.Modules.Catalog.Ports;

namespace PriceNegotiationApp.Modules.Catalog.Adapters;

// MA0182: used via DI registration (AddScoped<IProductPriceProvider, ProductPriceProvider>), invisible to static analysis.
#pragma warning disable MA0182
/// <summary>Adapter: Negotiations reads product price snapshots from Catalog's persistence.</summary>
internal sealed class ProductPriceProvider(CatalogDbContext db) : IProductPriceProvider
#pragma warning restore MA0182
{
    public async Task<ProductSnapshot?> GetAsync(Guid productId, CancellationToken ct) =>
        await db.Products.AsNoTracking()
            .Where(p => p.Id == ProductId.From(productId))
            .Select(p => new ProductSnapshot(productId, p.Price))
            .FirstOrDefaultAsync(ct);
}
