using PriceNegotiationApp.Domain.Models.Negotiations;

namespace PriceNegotiationApp.Application.Negotiations.Dto.Requests.UpdateNegotiation
{
    public record UpdateNegotiationRequestDto
    {
        public decimal ProposedPrice { get; init; }
    }
}
