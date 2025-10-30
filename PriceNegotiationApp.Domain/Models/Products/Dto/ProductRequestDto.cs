using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PriceNegotiationApp.Domain.Models.Products.Dto
{
	public class ProductRequestDto
	{
		[Required]
		[MinLength(1)]
		public string Name { get; init; }

		[Required]
		public decimal Price { get; init; }
	}
}
