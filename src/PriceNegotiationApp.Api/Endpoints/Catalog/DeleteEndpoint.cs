using PriceNegotiationApp.Modules.Catalog.Infrastructure.Delete;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Catalog.Application;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Api.Endpoints.Catalog.Delete;

internal static class DeleteEndpoint
{
    internal static void MapDelete(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", async (Guid id, DeleteProductHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(id, ct);
            return TypedResults.NoContent();
        })
        .RequireRoles(UserRoles.Admin);
    }
}
