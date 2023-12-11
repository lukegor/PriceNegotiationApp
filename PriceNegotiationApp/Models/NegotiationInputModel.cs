using System.ComponentModel.DataAnnotations;

namespace PriceNegotiationApp.Models
{
	public class NegotiationInputModel
	{
		[Required]
		public int ProductId { get; set; }
		[Required]
		public decimal ProposedPrice { get; set; }
	}
}
