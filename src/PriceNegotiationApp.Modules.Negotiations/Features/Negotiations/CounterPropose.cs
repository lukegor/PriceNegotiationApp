using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.SharedKernel;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.Modules.Negotiations.Persistence;
using System.Security.Claims;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal static class CounterPropose
{
    internal static void MapCounterPropose(this RouteGroupBuilder group)
    {
        group.MapPatch("/{id:guid}/proposals", async (Guid id, CounterProposalRequest request,
                ClaimsPrincipal principal, NegotiationsDbContext db, INegotiationPolicy policy,
                TimeProvider clock, CancellationToken ct) =>
            {
                var caller = principal.ToCallerContext();
                var negotiation = await NegotiationAccess.RequireOwnedAsync(db, caller, id, ct);

                var outcome = negotiation.CounterPropose(request.ProposedPrice, clock.GetUtcNow(), policy);
                if (outcome == NegotiationOutcome.NoProposalsRemaining)
                {
                    throw new ConflictException(NegotiationErrorCodes.NoProposalsRemaining,
                        "No proposals remain for this negotiation.");
                }

                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(new CounterProposalOutcome(outcome.ToString(),
                    NegotiationResponses.ToResponse(negotiation, policy)));
            })
        .RequireAuthorization();
    }
}

