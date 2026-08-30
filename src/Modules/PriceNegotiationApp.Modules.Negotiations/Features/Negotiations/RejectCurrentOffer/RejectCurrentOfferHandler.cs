using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;
using PriceNegotiationApp.Modules.Negotiations.Persistence;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.RejectCurrentOffer;

internal sealed class RejectCurrentOfferHandler(NegotiationsDbContext db, TimeProvider clock)
{
    public async Task<StaffActionResponse> HandleAsync(Guid id, CancellationToken ct)
    {
        var negotiation = await NegotiationAccess.RequireAsync(db, id, ct);
        negotiation.RejectCurrentOffer(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return new StaffActionResponse("current_offer_rejected",
            NegotiationResponses.ToResponse(negotiation));
    }
}
