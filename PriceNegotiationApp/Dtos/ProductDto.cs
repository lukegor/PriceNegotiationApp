using PriceNegotiationApp.Domain.Models.Products;

namespace PriceNegotiationApp.Dtos
{
    /// <summary>
    /// Represents a <see cref="Product"/> DTO for OData"/>
    /// </summary>
    public class ProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
    }
}
