
namespace PriceNegotiationApp.Contracts.Negotiations.Dto.Response
{
    public class ProposePriceResponseDto
    {
        public required string Result { get; init; }
        public decimal? MaxAllowedPrice { get; init; }
    }
}
