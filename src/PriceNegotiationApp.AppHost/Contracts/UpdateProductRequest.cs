namespace PriceNegotiationApp.AppHost.Contracts;

public sealed class UpdateProductRequest
{
    public string Name { get; init; } = string.Empty;

    public decimal Price { get; init; }
}

