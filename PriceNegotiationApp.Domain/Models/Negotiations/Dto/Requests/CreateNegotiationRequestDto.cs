using System.ComponentModel.DataAnnotations;

namespace PriceNegotiationApp.Domain.Models.Negotiations.Dto.Requests
{
	public class CreateNegotiationRequestDto
	{
		//[Required]
		public int ProductId { get; init; }
		//[Required]
		//[Range(0.01, double.MaxValue, ErrorMessage = "ProductPrice must be greater than or equal to 0.01")]
		public decimal ProposedPrice { get; init; }
	}
}
