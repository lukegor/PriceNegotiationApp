using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.Modules.Negotiations.Persistence;
using PriceNegotiationApp.SharedKernel;
using System.Security.Claims;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal static class Get
{
    internal static void MapGetOne(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal principal, NegotiationsDbContext db,
                CancellationToken ct) =>
            {
                var caller = principal.ToCallerContext();
                var negotiation = await NegotiationAccess.RequireReadOnlyAsync(db, id, ct);
                if (!await NegotiationAccess.CanAccessAsync(db, caller, negotiation, ct))
                {
                    throw new ForbiddenAccessException();
                }

                return TypedResults.Ok(NegotiationResponses.ToResponse(negotiation));
            })
        .RequireAuthorization();
    }
}
