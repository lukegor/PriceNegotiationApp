using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Application.Abstractions;
using PriceNegotiationApp.Domain.Models;
using PriceNegotiationApp.Domain.ValueObjects.Ids;

namespace PriceNegotiationApp.Infrastructure.Persistence.Repositories;

public sealed class NegotiationRepository(AppDbContext db, ICustomerRepository customers) : INegotiationRepository
{
    public Task<Negotiation?> GetAsync(NegotiationId id, CancellationToken ct) =>
        db.Negotiations.FirstOrDefaultAsync(n => n.Id == id, ct);

    public IQueryable<Negotiation> Query() => db.Negotiations.AsNoTracking();

    public async Task AddAsync(Negotiation negotiation, CancellationToken ct) =>
        await db.Negotiations.AddAsync(negotiation, ct);

    public async Task<Negotiation?> FindOpenAsync(ProductId productId, Guid identityUserId, CancellationToken ct)
    {
        var customer = await customers.GetByIdentityAsync(identityUserId, ct);
        if (customer is null)
        {
            return null;
        }

        return await db.Negotiations.FirstOrDefaultAsync(
            n => n.ProductId == productId && n.CustomerId == customer.Id && n.Status == NegotiationStatus.Open, ct);
    }

    public void Remove(Negotiation negotiation) => db.Negotiations.Remove(negotiation);
}
