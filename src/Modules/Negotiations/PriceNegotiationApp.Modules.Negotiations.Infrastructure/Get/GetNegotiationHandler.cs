using PriceNegotiationApp.Modules.Negotiations.Infrastructure;
using PriceNegotiationApp.Modules.Negotiations.Application;
using PriceNegotiationApp.Modules.Negotiations.Infrastructure.Persistence;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Negotiations.Infrastructure.Get;

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
