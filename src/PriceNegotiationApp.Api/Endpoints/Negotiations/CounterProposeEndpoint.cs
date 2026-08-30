using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.CounterPropose;
using PriceNegotiationApp.SharedKernel;
using System.Security.Claims;

namespace PriceNegotiationApp.Api.Endpoints.Negotiations.CounterPropose;

internal static class CounterProposeEndpoint
{
    internal static void MapCounterPropose(this RouteGroupBuilder group)
    {
        group.MapPatch("/{id:guid}/proposals", async (Guid id, CounterProposalRequest request,
                ClaimsPrincipal principal, CounterProposeHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(id, request, principal.ToCallerContext(), ct)));
    }
}
