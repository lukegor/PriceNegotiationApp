using Vogen;

namespace PriceNegotiationApp.Modules.Negotiations.Domain;

[ValueObject<Guid>(Conversions.None)]
internal readonly partial record struct CustomerId;

