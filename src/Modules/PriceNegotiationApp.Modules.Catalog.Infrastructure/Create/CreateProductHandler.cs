using PriceNegotiationApp.Modules.Catalog.Application.Create;
using PriceNegotiationApp.Modules.Catalog.Domain;
using PriceNegotiationApp.Modules.Catalog.Application;
using PriceNegotiationApp.Modules.Catalog.Infrastructure.Persistence;

namespace PriceNegotiationApp.Modules.Catalog.Infrastructure.Create;

internal sealed class CreateProductHandler(CatalogDbContext db)
{
    public async Task<ProductResponse> HandleAsync(CreateProductRequest request, CancellationToken ct)
    {
        var product = Product.Create(request.Name, request.Price);
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return new ProductResponse(product.Id.Value, product.Name, product.Price);
    }
}
