
namespace PriceNegotiationApp.Contracts.Negotiations.Dto.Requests
{
    public record CreateNegotiationRequestDto
    {
        public Guid ProductId { get; init; }
        public decimal ProposedPrice { get; init; }
    }
}
