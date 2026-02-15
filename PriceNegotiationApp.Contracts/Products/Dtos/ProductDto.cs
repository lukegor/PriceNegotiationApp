namespace PriceNegotiationApp.Contracts.Products.Dtos
{
    /// <summary>
    /// Represents a <see cref="Product"/> Data Transfer Object (DTO) for OData"/>
    /// </summary>
    public class ProductDto
    {
        public Guid Id { get; init; }
        public required string Name { get; init; }
        public decimal Price { get; init; }
    }
}
