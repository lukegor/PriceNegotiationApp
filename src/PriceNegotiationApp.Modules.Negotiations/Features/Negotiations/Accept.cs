using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.SharedKernel;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.Modules.Negotiations.Persistence;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal static class Accept
{
    internal static void MapAccept(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/accept", async (Guid id, NegotiationsDbContext db,
                INegotiationPolicy policy, TimeProvider clock, CancellationToken ct) =>
            {
                var negotiation = await NegotiationAccess.RequireAsync(db, id, ct);
                negotiation.Accept(clock.GetUtcNow());
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(NegotiationResponses.ToResponse(negotiation, policy));
            })
        .RequireRoles(UserRoles.Admin, UserRoles.Staff);
    }
}



