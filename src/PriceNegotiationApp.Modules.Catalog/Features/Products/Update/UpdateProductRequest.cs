namespace PriceNegotiationApp.Modules.Catalog.Features.Products.Update;

internal sealed class UpdateProductRequest
{
    public string Name { get; init; } = string.Empty;

    public decimal Price { get; init; }
}
