using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.SharedKernel;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.Modules.Negotiations.Persistence;

namespace PriceNegotiationApp.Modules.Negotiations.Features;

internal static class List
{
    internal static void MapList(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (NegotiationsDbContext db, INegotiationPolicy policy,
                CancellationToken ct, int page = 1, int pageSize = 20) =>
            {
                var query = new PageQuery(page, pageSize);
                var q = db.Negotiations.AsNoTracking();
                var total = await q.LongCountAsync(ct);
                var items = await q.OrderByDescending(n => n.CreatedAtUtc)
                    .Skip(query.Skip).Take(query.SafePageSize)
                    .ToListAsync(ct);
                return TypedResults.Ok(new PagedResult<NegotiationResponse>(
                    items.Select(n => NegotiationResponses.ToResponse(n, policy)).ToList(),
                    query.SafePage, query.SafePageSize, total));
            })
        .RequireRoles(UserRoles.Admin, UserRoles.Staff);
    }
}


