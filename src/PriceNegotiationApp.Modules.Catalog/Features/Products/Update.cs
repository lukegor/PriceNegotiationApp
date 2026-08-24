using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Modules.Catalog.Domain;
using PriceNegotiationApp.Modules.Catalog.Persistence;

namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

internal static class Update
{
    internal static void MapUpdate(this RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", async (Guid id, UpdateProductRequest request, CatalogDbContext db,
                CancellationToken ct) =>
            {
                var product = await db.Products.FirstOrDefaultAsync(p => p.Id == ProductId.From(id), ct)
                              ?? throw new NotFoundException("Product", id);
                product.Update(request.Name, request.Price);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(new ProductResponse(product.Id.Value, product.Name, product.Price));
            })
        .RequireRoles(UserRoles.Admin, UserRoles.Staff);
    }
}
