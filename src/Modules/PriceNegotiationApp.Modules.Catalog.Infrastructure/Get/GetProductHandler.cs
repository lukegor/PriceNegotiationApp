using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Modules.Catalog.Domain;
using PriceNegotiationApp.Modules.Catalog.Application;
using PriceNegotiationApp.Modules.Catalog.Infrastructure.Persistence;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Catalog.Infrastructure.Get;

internal sealed class GetProductHandler(CatalogDbContext db)
{
    public async Task<ProductResponse> HandleAsync(Guid id, CancellationToken ct) =>
        await db.Products.AsNoTracking()
            .Where(p => p.Id == ProductId.From(id))
            .Select(p => new ProductResponse(p.Id.Value, p.Name, p.Price))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Product", id);
}
