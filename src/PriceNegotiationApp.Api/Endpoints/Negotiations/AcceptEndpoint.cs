using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.Accept;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Api.Endpoints.Negotiations.Accept;

internal static class AcceptEndpoint
{
    internal static void MapAccept(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/accept", async (Guid id, AcceptHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(id, ct)))
        .RequireRoles(UserRoles.Admin, UserRoles.Staff);
    }
}
