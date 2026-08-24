using Vogen;

namespace PriceNegotiationApp.Modules.Negotiations.Domain;

[ValueObject<Guid>(Conversions.None)]
public readonly partial record struct CustomerId;

