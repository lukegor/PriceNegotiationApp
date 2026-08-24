using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Modules.Negotiations.Persistence;
using System.Security.Claims;

namespace PriceNegotiationApp.Modules.Negotiations.Features;

internal static class Withdraw
{
    internal static void MapWithdraw(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal principal, NegotiationsDbContext db,
                CancellationToken ct) =>
            {
                var caller = principal.ToCallerContext();
                var negotiation = await NegotiationAccess.RequireAsync(db, id, ct);
                if (!caller.IsInRole(UserRoles.Admin)
                    && !await NegotiationAccess.IsOwnerAsync(db, caller.UserId, negotiation, ct))
                {
                    throw new ForbiddenAccessException();
                }

                db.Negotiations.Remove(negotiation);
                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
        .RequireAuthorization();
    }
}

