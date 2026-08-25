using PriceNegotiationApp.Modules.Catalog.Domain;

using PriceNegotiationApp.Modules.Catalog.Persistence;

namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

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
