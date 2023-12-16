using System.ComponentModel.DataAnnotations;

namespace PriceNegotiationApp.Models.Input_Models
{
	public class NegotiationInputModel
	{
		[Required]
		public int ProductId { get; set; }
		[Required]
		[Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than or equal to 0.01")]
		public decimal ProposedPrice { get; set; }
		[Required]
		public string UserId { get; set; }
	}
}
