using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;
using PriceNegotiationApp.Modules.Negotiations.Persistence;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.Accept;

internal sealed class AcceptHandler(NegotiationsDbContext db, TimeProvider clock)
{
    public async Task<StaffActionResponse> HandleAsync(Guid id, CancellationToken ct)
    {
        var negotiation = await NegotiationAccess.RequireAsync(db, id, ct);
        negotiation.Accept(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return new StaffActionResponse("accepted", NegotiationResponses.ToResponse(negotiation));
    }
}
