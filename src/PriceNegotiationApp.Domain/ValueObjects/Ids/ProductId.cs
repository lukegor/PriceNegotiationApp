using Vogen;

namespace PriceNegotiationApp.Domain.ValueObjects.Ids;

[ValueObject<Guid>(Conversions.None)]
public readonly partial record struct ProductId;

