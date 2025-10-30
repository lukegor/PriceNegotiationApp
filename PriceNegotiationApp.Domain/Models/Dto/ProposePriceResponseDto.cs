using PriceNegotiationApp.Utility.Utility;

namespace PriceNegotiationApp.Domain.Models.Dto
{
	public class ProposePriceResponseDto
	{
		public ProposePriceResult Result { get; init; }
		public decimal? MaxAllowedPrice { get; init; }
	}
}
