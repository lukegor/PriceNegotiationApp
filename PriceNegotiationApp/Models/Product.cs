using System.ComponentModel.DataAnnotations;

namespace PriceNegotiationApp.Models
{
	public class Product
	{
		public int Id { get; set; }

		[Required]
		public string Name { get; set; }

		[Range(0, double.MaxValue, ErrorMessage = "Price must be greater than or equal to 0")]
		public decimal Price { get; set; }
	}
}
