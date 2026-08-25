using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Negotiations.Persistence;
using PriceNegotiationApp.SharedKernel;
using System.Security.Claims;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal static class Withdraw
{
    internal static void MapWithdraw(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal principal, NegotiationsDbContext db,
                TimeProvider clock, CancellationToken ct) =>
            {
                var caller = principal.ToCallerContext();
                var negotiation = await NegotiationAccess.RequireAsync(db, id, ct);

                if (caller.IsInRole(UserRoles.Admin))
                {
                    db.Negotiations.Remove(negotiation);
                }
                else
                {
                    if (!await NegotiationAccess.IsOwnerAsync(db, caller.UserId, negotiation, ct))
                    {
                        throw new ForbiddenAccessException();
                    }

                    negotiation.Withdraw(clock.GetUtcNow());
                }

                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
        .RequireAuthorization();
    }
}
