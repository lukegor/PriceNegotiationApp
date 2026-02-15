using PriceNegotiationApp.Domain.Models.Negotiations;

namespace PriceNegotiationApp.Application.Negotiations.Dtos
{
    public record NegotiationResultDto(
        Guid NegotiationId,
        Guid ProductId,
        decimal ProposedPrice,
        bool IsAccepted,
        int RetriesLeft,
        NegotiationStatus Status,
        Guid UserId);
}
