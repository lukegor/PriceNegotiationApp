namespace PriceNegotiationApp.Modules.Catalog.Application.Create;

internal sealed class CreateProductRequest
{
    public string Name { get; init; } = string.Empty;

    public decimal Price { get; init; }
}
