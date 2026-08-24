using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.SharedKernel;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.Modules.Negotiations.Persistence;

namespace PriceNegotiationApp.Modules.Negotiations.Features;

internal static class Decline
{
    internal static void MapDecline(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/decline", async (Guid id, NegotiationsDbContext db,
                INegotiationPolicy policy, CancellationToken ct) =>
            {
                var negotiation = await NegotiationAccess.RequireAsync(db, id, ct);
                negotiation.Decline();
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(NegotiationResponses.ToResponse(negotiation, policy));
            })
        .RequireRoles(UserRoles.Admin, UserRoles.Staff);
    }
}



