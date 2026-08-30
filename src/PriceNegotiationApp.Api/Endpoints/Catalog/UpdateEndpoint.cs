using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Catalog.Features.Products.Update;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Api.Endpoints.Catalog.Update;

internal static class UpdateEndpoint
{
    internal static void MapUpdate(this RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", async (Guid id, UpdateProductRequest request,
                UpdateProductHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(id, request, ct)))
        .RequireRoles(UserRoles.Admin, UserRoles.Staff);
    }
}
