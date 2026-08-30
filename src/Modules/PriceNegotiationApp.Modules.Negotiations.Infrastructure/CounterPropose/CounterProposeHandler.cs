using PriceNegotiationApp.Modules.Negotiations.Contracts;
using PriceNegotiationApp.Modules.Negotiations.Infrastructure;
using PriceNegotiationApp.Modules.Negotiations.Application.CounterPropose;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.Modules.Negotiations.Application;
using PriceNegotiationApp.Modules.Negotiations.Infrastructure.Persistence;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Negotiations.Infrastructure.CounterPropose;

internal sealed class CounterProposeHandler(NegotiationsDbContext db, TimeProvider clock)
{
    public async Task<CounterProposalResponse> HandleAsync(
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
        return new CounterProposalResponse(outcome.ToString(), NegotiationResponses.ToResponse(negotiation));
    }
}
