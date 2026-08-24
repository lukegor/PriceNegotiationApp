namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

internal sealed class CreateProductRequest
{
    public string Name { get; init; } = string.Empty;

    public decimal Price { get; init; }
}

internal sealed class UpdateProductRequest
{
    public string Name { get; init; } = string.Empty;

    public decimal Price { get; init; }
}

internal sealed record ProductResponse(Guid Id, string Name, decimal Price);
