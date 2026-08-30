using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Modules.Catalog.Domain;
using PriceNegotiationApp.Modules.Catalog.Persistence;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Catalog.Features.Products.Delete;

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
