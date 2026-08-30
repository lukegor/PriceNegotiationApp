namespace PriceNegotiationApp.Modules.Catalog.Contracts;

public interface IProductPriceProvider
{
    /// <summary>Returns null when the product does not exist.</summary>
    Task<ProductSnapshot?> GetAsync(Guid productId, CancellationToken ct);
}

public readonly record struct ProductSnapshot(Guid ProductId, decimal Price);
