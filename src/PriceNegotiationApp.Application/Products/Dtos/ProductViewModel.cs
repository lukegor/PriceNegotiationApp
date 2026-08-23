namespace PriceNegotiationApp.Application.Products.Dtos
{
    public record ProductViewModel(
        Guid Id,
        string Name,
        decimal Price);
}
