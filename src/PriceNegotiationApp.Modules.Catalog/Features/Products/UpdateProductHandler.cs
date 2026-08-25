using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Modules.Catalog.Domain;
using PriceNegotiationApp.Modules.Catalog.Persistence;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

internal sealed class UpdateProductHandler(CatalogDbContext db)
{
    public async Task<ProductResponse> HandleAsync(Guid id, UpdateProductRequest request, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == ProductId.From(id), ct)
                      ?? throw new NotFoundException("Product", id);

        product.Update(request.Name, request.Price);
        await db.SaveChangesAsync(ct);
        return new ProductResponse(product.Id.Value, product.Name, product.Price);
    }
}
