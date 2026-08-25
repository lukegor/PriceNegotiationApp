using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.SharedKernel;
using System.Security.Claims;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal static class Get
{
    internal static void MapGetOne(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal principal,
                GetNegotiationHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(id, principal.ToCallerContext(), ct)))
        .RequireAuthorization();
    }
}
