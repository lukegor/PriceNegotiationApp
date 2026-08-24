using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.Modules.Negotiations.Persistence;
using PriceNegotiationApp.SharedKernel;
using System.Security.Claims;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal static class ListMine
{
    internal static void MapListMine(this RouteGroupBuilder group)
    {
        group.MapGet("/mine", async (ClaimsPrincipal principal, NegotiationsDbContext db,
                INegotiationPolicy policy, CancellationToken ct, int page = 1, int pageSize = 20) =>
            {
                var caller = principal.ToCallerContext();
                var query = new PageQuery(page, pageSize);
                var customer = await NegotiationAccess.CustomerByIdentityAsync(db, caller.UserId, ct);
                var q = db.Negotiations.AsNoTracking().Where(n => customer != null && n.CustomerId == customer.Id);
                var total = await q.LongCountAsync(ct);
                var items = await q.OrderByDescending(n => n.CreatedAtUtc)
                    .Skip(query.Skip).Take(query.SafePageSize)
                    .ToListAsync(ct);
                return TypedResults.Ok(new PagedResult<NegotiationResponse>(
                    items.Select(n => NegotiationResponses.ToResponse(n, policy)).ToList(),
                    query.SafePage, query.SafePageSize, total));
            })
        .RequireRoles(UserRoles.Customer);
    }
}


