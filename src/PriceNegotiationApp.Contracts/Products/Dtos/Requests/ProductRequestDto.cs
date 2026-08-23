namespace PriceNegotiationApp.Contracts.Products.Dtos.Requests
{
    public class ProductRequestDto
    {
        public required string Name { get; init; }
        public decimal Price { get; init; }
    }
}
