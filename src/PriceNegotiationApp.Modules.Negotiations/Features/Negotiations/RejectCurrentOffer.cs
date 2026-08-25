using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Negotiations.Persistence;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal static class RejectCurrentOffer
{
    internal static void MapRejectCurrentOffer(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/decline", async (Guid id, NegotiationsDbContext db,
                TimeProvider clock, CancellationToken ct) =>
            {
                var negotiation = await NegotiationAccess.RequireAsync(db, id, ct);
                negotiation.RejectCurrentOffer(clock.GetUtcNow());
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(new StaffActionResponse("current_offer_rejected",
                    NegotiationResponses.ToResponse(negotiation)));
            })
        .RequireRoles(UserRoles.Admin, UserRoles.Staff);
    }
}
