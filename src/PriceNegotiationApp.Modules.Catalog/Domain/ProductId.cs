using Vogen;

namespace PriceNegotiationApp.Modules.Catalog.Domain;

[ValueObject<Guid>(Conversions.None)]
public readonly partial record struct ProductId;


