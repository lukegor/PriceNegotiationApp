namespace PriceNegotiationApp.Application.Negotiations.Dtos
{
    public record NegotiationViewModel(
        Guid Id,
        Guid ProductId,
        string ProductName,
        Guid CustomerId,
        string CustomerName,
        decimal ProposedPrice,
        string Status,
        int RetriesLeft,
        decimal MaxAllowedPrice);
}
