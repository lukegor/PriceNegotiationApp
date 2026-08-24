using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.SharedKernel;
using PriceNegotiationApp.Modules.Catalog.Domain;
using PriceNegotiationApp.Modules.Catalog.Persistence;

namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

internal static class Create
{
    internal static void MapCreate(this RouteGroupBuilder group)
    {
        group.MapPost("/", async (CreateProductRequest request, CatalogDbContext db, CancellationToken ct) =>
            {
                var product = Product.Create(request.Name, request.Price);
                await db.Products.AddAsync(product, ct);
                await db.SaveChangesAsync(ct);
                return TypedResults.CreatedAtRoute(
                    new ProductResponse(product.Id.Value, product.Name, product.Price),
                    "GetProductById", new { id = product.Id.Value });
            })
        .RequireRoles(UserRoles.Admin, UserRoles.Staff);
    }
}

