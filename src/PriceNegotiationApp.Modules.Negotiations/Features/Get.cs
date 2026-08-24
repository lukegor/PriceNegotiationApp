using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.Modules.Negotiations.Persistence;
using System.Security.Claims;

namespace PriceNegotiationApp.Modules.Negotiations.Features;

internal static class Get
{
    internal static void MapGetOne(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal principal, NegotiationsDbContext db,
                INegotiationPolicy policy, CancellationToken ct) =>
            {
                var caller = principal.ToCallerContext();
                var negotiation = await NegotiationAccess.RequireAsync(db, id, ct);
                if (!await NegotiationAccess.CanAccessAsync(db, caller, negotiation, ct))
                {
                    throw new ForbiddenAccessException();
                }

                return TypedResults.Ok(NegotiationResponses.ToResponse(negotiation, policy));
            })
        .RequireAuthorization();
    }
}
