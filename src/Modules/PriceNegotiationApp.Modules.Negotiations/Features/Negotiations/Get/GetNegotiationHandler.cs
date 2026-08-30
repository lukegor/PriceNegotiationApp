using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;
using PriceNegotiationApp.Modules.Negotiations.Persistence;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.Get;

internal sealed class GetNegotiationHandler(NegotiationsDbContext db)
{
    public async Task<NegotiationResponse> HandleAsync(Guid id, CallerContext caller, CancellationToken ct)
    {
        var negotiation = await NegotiationAccess.RequireReadOnlyAsync(db, id, ct);
        if (!await NegotiationAccess.CanAccessAsync(db, caller, negotiation, ct))
        {
            throw new ForbiddenAccessException();
        }

        return NegotiationResponses.ToResponse(negotiation);
    }
}
