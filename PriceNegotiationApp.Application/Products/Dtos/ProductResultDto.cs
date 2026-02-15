namespace PriceNegotiationApp.Application.Products.Dtos
{
    public record ProductResultDto(
        Guid Id,
        string Name,
        decimal Price);
}
