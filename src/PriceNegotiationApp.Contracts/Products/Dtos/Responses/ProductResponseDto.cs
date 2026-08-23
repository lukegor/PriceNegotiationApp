namespace PriceNegotiationApp.Contracts.Products.Dtos.Responses
{
    public record ProductResponseDto(
        Guid Id,
        string Name,
        decimal Price);
}
