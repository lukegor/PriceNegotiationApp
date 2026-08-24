namespace PriceNegotiationApp.AppHost.Contracts;

public sealed class CreateProductRequest
{
    public string Name { get; init; } = string.Empty;

    public decimal Price { get; init; }
}

