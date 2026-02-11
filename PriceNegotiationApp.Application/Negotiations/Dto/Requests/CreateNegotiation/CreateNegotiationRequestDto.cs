using PriceNegotiationApp.Domain.Models.Products;

namespace PriceNegotiationApp.Application.Negotiations.Dto.Requests.CreateNegotiation
{
    public record CreateNegotiationRequestDto
    {
        public ProductId ProductId { get; init; }
        public decimal ProposedPrice { get; init; }
    }
}
