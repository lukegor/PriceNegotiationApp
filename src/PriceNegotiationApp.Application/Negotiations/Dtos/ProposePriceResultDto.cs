namespace PriceNegotiationApp.Application.Negotiations.Dtos
{
    public class ProposePriceResultDto
    {
        public ProposePriceResult Result { get; init; }
        public decimal? MaxAllowedPrice { get; init; }
    }
}
