using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Modules.Catalog.Domain;
using PriceNegotiationApp.Modules.Catalog.Persistence;

namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

internal static class Delete
{
    internal static void MapDelete(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", async (Guid id, CatalogDbContext db, CancellationToken ct) =>
            {
                var product = await db.Products.FirstOrDefaultAsync(p => p.Id == ProductId.From(id), ct)
                              ?? throw new NotFoundException("Product", id);
                // Negotiations survive on their snapshots by design (spec §6).
                db.Products.Remove(product);
                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
        .RequireRoles(UserRoles.Admin);
    }
}

