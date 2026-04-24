namespace PriceNegotiationApp.Contracts.Products.Dtos
{
    /// <summary>
    /// Represents a <see cref="Product"/> Data Transfer Object (DTO) for OData"/>
    /// </summary>
    public record ProductDto(
        Guid Id,
        string Name,
        decimal Price);
}
