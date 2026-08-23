using PriceNegotiationApp.Domain.Abstractions;
using PriceNegotiationApp.Domain.ValueObjects.Ids;

namespace PriceNegotiationApp.Domain.Models;

public sealed class Customer : Entity
{
    public CustomerId Id { get; private set; }

    public Guid IdentityUserId { get; private set; }

    private Customer()
    {
    }

    private Customer(CustomerId id, Guid identityUserId)
    {
        Id = id;
        IdentityUserId = identityUserId;
    }

    public static Customer Create(Guid identityUserId) =>
        new(CustomerId.From(Guid.CreateVersion7()), identityUserId);
}
