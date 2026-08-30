namespace PriceNegotiationApp.Modules.Catalog.Features.Products.Create;

internal sealed class CreateProductRequest
{
    public string Name { get; init; } = string.Empty;

    public decimal Price { get; init; }
}
