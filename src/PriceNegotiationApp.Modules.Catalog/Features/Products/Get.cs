using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

internal static class Get
{
    internal static void MapGetOne(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", async (Guid id, GetProductHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(id, ct)))
            .WithName("GetProductById")
            .CacheOutput(Policies.ShortCachePolicy)
            .AllowAnonymous();
    }
}
