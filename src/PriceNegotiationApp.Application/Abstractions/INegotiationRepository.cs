using PriceNegotiationApp.Domain.Models;
using PriceNegotiationApp.Domain.ValueObjects.Ids;

namespace PriceNegotiationApp.Application.Abstractions;

public interface INegotiationRepository
{
    Task<Negotiation?> GetAsync(NegotiationId id, CancellationToken ct);

    IQueryable<Negotiation> Query();

    Task AddAsync(Negotiation negotiation, CancellationToken ct);

    Task<Negotiation?> FindOpenAsync(ProductId productId, Guid identityUserId, CancellationToken ct);

    void Remove(Negotiation negotiation);
}
