using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.Modules.Negotiations.Persistence;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal sealed class CounterProposeHandler(NegotiationsDbContext db, TimeProvider clock)
{
    public async Task<CounterProposalOutcome> HandleAsync(
        Guid id, CounterProposalRequest request, CallerContext caller, CancellationToken ct)
    {
        var negotiation = await NegotiationAccess.RequireOwnedAsync(db, caller, id, ct);

        var outcome = negotiation.CounterPropose(request.ProposedPrice, clock.GetUtcNow());
        if (outcome == NegotiationOutcome.NoProposalsRemaining)
        {
            throw new ConflictException(NegotiationErrorCodes.NoProposalsRemaining,
                "No proposals remain for this negotiation.");
        }

        await db.SaveChangesAsync(ct);
        return new CounterProposalOutcome(outcome.ToString(), NegotiationResponses.ToResponse(negotiation));
    }
}
