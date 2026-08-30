using PriceNegotiationApp.Modules.Negotiations.Infrastructure.CounterPropose;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Api;
using PriceNegotiationApp.Modules.Negotiations.Application.CounterPropose;
using PriceNegotiationApp.SharedKernel;
using System.Security.Claims;

namespace PriceNegotiationApp.Api.Endpoints.Negotiations.CounterPropose;

internal static class CounterProposeEndpoint
{
    internal static void MapCounterPropose(this RouteGroupBuilder group)
    {
        group.MapPatch("/{id:guid}/proposals", async (Guid id, CounterProposalRequest request,
                ClaimsPrincipal principal, CounterProposeHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(id, request, principal.ToCallerContext(), ct)))
        .AddEndpointFilter<ValidateRequestFilter<CounterProposalRequest>>();
    }
}
