namespace PriceNegotiationApp.Modules.Catalog.Application;

internal sealed record ProductResponse(Guid Id, string Name, decimal Price);
