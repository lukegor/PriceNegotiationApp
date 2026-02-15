namespace PriceNegotiationApp.Contracts.Negotiations.Dto.Response
{
    public record NegotiationResponseDto(
        Guid NegotiationId,
        Guid ProductId,
        decimal ProposedPrice,
        bool IsAccepted,
        int RetriesLeft,
        string Status,
        Guid UserId);
}
