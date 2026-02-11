using PriceNegotiationApp.Domain.Models.Negotiations;

namespace PriceNegotiationApp.Application.Negotiations.Dto.Response
{
    public record NegotiationResponseDto(
        Guid NegotiationId,
        Guid ProductId,
        decimal ProposedPrice,
        bool IsAccepted,
        int RetriesLeft,
        NegotiationStatus Status,
        Guid UserId);
}
