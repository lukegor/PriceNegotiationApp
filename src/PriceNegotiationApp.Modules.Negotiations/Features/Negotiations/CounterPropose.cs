using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.SharedKernel;
using System.Security.Claims;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal static class CounterPropose
{
    internal static void MapCounterPropose(this RouteGroupBuilder group)
    {
        group.MapPatch("/{id:guid}/proposals", async (Guid id, CounterProposalRequest request,
                ClaimsPrincipal principal, CounterProposeHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(id, request, principal.ToCallerContext(), ct)));
    }
}
