using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Modules.Catalog.Persistence;

namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

internal static class List
{
    internal static void MapList(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (CatalogDbContext db, CancellationToken ct,
                string? search = null, decimal? minPrice = null, decimal? maxPrice = null,
                string? sortBy = null, bool sortDesc = false, int page = 1, int pageSize = 20) =>
                TypedResults.Ok(await SearchAsync(db,
                    new ProductQuery(search, minPrice, maxPrice, sortBy, sortDesc, page, pageSize), ct)))
            .CacheOutput(Policies.ShortCachePolicy)
            .AllowAnonymous();
    }

    internal static async Task<PagedResult<ProductResponse>> SearchAsync(
        CatalogDbContext db, ProductQuery query, CancellationToken ct)
    {
        var page = new PageQuery(query.Page, query.PageSize);
        var q = db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            q = q.Where(p => EF.Functions.ILike(p.Name, $"%{query.Search.Trim()}%"));
        }

        if (query.MinPrice.HasValue)
        {
            q = q.Where(p => p.Price >= query.MinPrice.Value);
        }

        if (query.MaxPrice.HasValue)
        {
            q = q.Where(p => p.Price <= query.MaxPrice.Value);
        }

        var sortBy = query.SortBy?.Trim().ToLowerInvariant();
        q = (sortBy, query.SortDesc) switch
        {
            ("price", false) => q.OrderBy(p => p.Price),
            ("price", true) => q.OrderByDescending(p => p.Price),
            (_, true) => q.OrderByDescending(p => p.Name),
            _ => q.OrderBy(p => p.Name),
        };

        var total = await q.LongCountAsync(ct);
        var items = await q
            .Skip(page.Skip)
            .Take(page.SafePageSize)
            .Select(p => new ProductResponse(p.Id.Value, p.Name, p.Price))
            .ToListAsync(ct);

        return new PagedResult<ProductResponse>(items, page.SafePage, page.SafePageSize, total);
    }
}


