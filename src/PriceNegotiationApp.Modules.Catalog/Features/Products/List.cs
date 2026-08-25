using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

internal static class List
{
    internal static void MapList(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (ListProductsHandler handler, CancellationToken ct,
                string? search = null, decimal? minPrice = null, decimal? maxPrice = null,
                string? sortBy = null, bool sortDesc = false, int page = 1, int pageSize = 20) =>
                TypedResults.Ok(await handler.HandleAsync(
                    new ProductQuery(search, minPrice, maxPrice, sortBy, sortDesc, page, pageSize), ct)))
            .CacheOutput(Policies.ShortCachePolicy)
            .AllowAnonymous();
    }
}
