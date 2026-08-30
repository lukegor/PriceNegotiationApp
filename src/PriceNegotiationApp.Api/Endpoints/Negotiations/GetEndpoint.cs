using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.Get;
using PriceNegotiationApp.SharedKernel;
using System.Security.Claims;

namespace PriceNegotiationApp.Api.Endpoints.Negotiations.Get;

internal static class GetEndpoint
{
    internal static void MapGetOne(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal principal,
                GetNegotiationHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(id, principal.ToCallerContext(), ct)))
            .WithName("GetNegotiationById");
    }
}
