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
                CancellationToken ct, int page = 1, int pageSize = 20) =>
            {
                var caller = principal.ToCallerContext();
                var query = new PageQuery(page, pageSize);
                var customer = await NegotiationAccess.CustomerByIdentityAsync(db, caller.UserId, ct);
                if (customer is null)
                {
                    return TypedResults.Ok(new PagedResult<NegotiationResponse>(
                        [], query.SafePage, query.SafePageSize, 0));
                }

                var q = db.Negotiations.AsNoTracking().Where(n => n.CustomerId == customer.Id);
                var total = await q.LongCountAsync(ct);
                var items = await q.OrderByDescending(n => n.CreatedAtUtc)
                    .Skip(query.Skip).Take(query.SafePageSize)
                    .ToListAsync(ct);
                return TypedResults.Ok(new PagedResult<NegotiationResponse>(
                    items.Select(NegotiationResponses.ToResponse).ToList(),
                    query.SafePage, query.SafePageSize, total));
            })
        .RequireRoles(UserRoles.Customer);
    }
}


