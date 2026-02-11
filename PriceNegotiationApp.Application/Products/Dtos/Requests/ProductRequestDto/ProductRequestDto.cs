namespace PriceNegotiationApp.Application.Products.Dtos.Requests.ProductRequestDto
{
    public class ProductRequestDto
    {
        public required string Name { get; init; }
        public decimal Price { get; init; }
    }
}
