using PriceNegotiationApp.Utility;

namespace PriceNegotiationApp.Models
{
	public class ProposePriceResponse
	{
		public ProposePriceResult Result { get; set; }
		public decimal? MaxAllowedPrice { get; set; }
	}
}
