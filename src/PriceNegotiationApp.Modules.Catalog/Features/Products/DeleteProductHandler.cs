using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Modules.Catalog.Domain;
using PriceNegotiationApp.SharedKernel;

using PriceNegotiationApp.Modules.Catalog.Persistence;

namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

internal sealed class DeleteProductHandler(CatalogDbContext db)
{
    // Negotiations survive on their snapshots by design.
    public async Task HandleAsync(Guid id, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == ProductId.From(id), ct)
                      ?? throw new NotFoundException("Product", id);
        db.Products.Remove(product);
        await db.SaveChangesAsync(ct);
    }
}
