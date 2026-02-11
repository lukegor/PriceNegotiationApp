using PriceNegotiationApp.Application.Negotiations.Dto.Response;

namespace PriceNegotiationApp.Application.Common.Identities.Dtos.Responses
{
    public class ProposePriceResponseDto
    {
        public ProposePriceResultResponseDto Result { get; init; }
        public decimal? MaxAllowedPrice { get; init; }
    }
}
