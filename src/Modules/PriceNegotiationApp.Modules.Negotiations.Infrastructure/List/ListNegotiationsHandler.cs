using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Modules.Negotiations.Application;
using PriceNegotiationApp.Modules.Negotiations.Infrastructure.Persistence;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Negotiations.Infrastructure.List;

internal sealed class ListNegotiationsHandler(NegotiationsDbContext db)
{
    public async Task<PagedResult<NegotiationResponse>> HandleAsync(PageQuery page, CancellationToken ct)
    {
        var q = db.Negotiations.AsNoTracking();
        var total = await q.LongCountAsync(ct);
        var items = await q.OrderByDescending(n => n.CreatedAtUtc)
            .Skip(page.Skip).Take(page.SafePageSize)
            .ToListAsync(ct);

        return new PagedResult<NegotiationResponse>(
            items.Select(NegotiationResponses.ToResponse).ToList(),
            page.SafePage, page.SafePageSize, total);
    }
}
