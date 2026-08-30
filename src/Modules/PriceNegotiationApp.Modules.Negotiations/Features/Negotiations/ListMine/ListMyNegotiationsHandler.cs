using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;
using PriceNegotiationApp.Modules.Negotiations.Persistence;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.ListMine;

internal sealed class ListMyNegotiationsHandler(NegotiationsDbContext db)
{
    public async Task<PagedResult<NegotiationResponse>> HandleAsync(
        PageQuery page, CallerContext caller, CancellationToken ct)
    {
        var customer = await NegotiationAccess.CustomerByIdentityAsync(db, caller.UserId, ct);
        if (customer is null)
        {
            return new PagedResult<NegotiationResponse>([], page.SafePage, page.SafePageSize, 0);
        }

        var q = db.Negotiations.AsNoTracking().Where(n => n.CustomerId == customer.Id);
        var total = await q.LongCountAsync(ct);
        var items = await q.OrderByDescending(n => n.CreatedAtUtc)
            .Skip(page.Skip).Take(page.SafePageSize)
            .ToListAsync(ct);

        return new PagedResult<NegotiationResponse>(
            items.Select(NegotiationResponses.ToResponse).ToList(),
            page.SafePage, page.SafePageSize, total);
    }
}
