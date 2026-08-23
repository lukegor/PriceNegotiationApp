namespace PriceNegotiationApp.Application.Responses;

public sealed record ProductResponse(Guid Id, string Name, decimal Price);
