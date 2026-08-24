using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Modules.Catalog.Domain;
using PriceNegotiationApp.Modules.Catalog.Persistence;

namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

internal static class Get
{
    internal static void MapGetOne(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", async (Guid id, CatalogDbContext db, CancellationToken ct) =>
                TypedResults.Ok(await RequireAsync(db, id, ct)))
            .WithName("GetProductById")
            .CacheOutput(Policies.ShortCachePolicy)
            .AllowAnonymous();
    }

    internal static async Task<ProductResponse> RequireAsync(CatalogDbContext db, Guid id, CancellationToken ct) =>
        await db.Products.AsNoTracking()
            .Where(p => p.Id == ProductId.From(id))
            .Select(p => new ProductResponse(p.Id.Value, p.Name, p.Price))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Product", id);
}


