using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

internal static class Create
{
    internal static void MapCreate(this RouteGroupBuilder group)
    {
        group.MapPost("/", async (CreateProductRequest request, CreateProductHandler handler,
                CancellationToken ct) =>
            {
                var response = await handler.HandleAsync(request, ct);
                return TypedResults.CreatedAtRoute(response, "GetProductById", new { id = response.Id });
            })
        .RequireRoles(UserRoles.Admin, UserRoles.Staff);
    }
}
