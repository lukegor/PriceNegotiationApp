using PriceNegotiationApp.Domain.Models.Products;

namespace PriceNegotiationApp.Application.Products.Dtos
{
    /// <summary>
    /// Represents a <see cref="Product"/> Data Transfer Object (DTO) for OData"/>
    /// </summary>
    public class ProductDto
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public decimal Price { get; set; }
    }
}
