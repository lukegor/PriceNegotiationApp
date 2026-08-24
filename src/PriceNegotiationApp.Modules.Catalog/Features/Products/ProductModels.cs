namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

public sealed class CreateProductRequest
{
    public string Name { get; init; } = string.Empty;

    public decimal Price { get; init; }
}

public sealed class UpdateProductRequest
{
    public string Name { get; init; } = string.Empty;

    public decimal Price { get; init; }
}

public sealed record ProductResponse(Guid Id, string Name, decimal Price);
