using PriceNegotiationApp.Domain.Models;
using PriceNegotiationApp.Domain.ValueObjects.Ids;

namespace PriceNegotiationApp.Application.Abstractions;

public interface ICustomerRepository
{
    Task<CustomerId> GetOrCreateAsync(Guid identityUserId, CancellationToken ct);

    Task<Customer?> GetByIdentityAsync(Guid identityUserId, CancellationToken ct);
}
