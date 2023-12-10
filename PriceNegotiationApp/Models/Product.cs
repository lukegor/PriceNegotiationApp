using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PriceNegotiationApp.Models
{
	public class Product
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int Id { get; set; }

		[Required]
		public string Name { get; set; }

		[Range(0, double.MaxValue, ErrorMessage = "Price must be greater than or equal to 0")]
		public decimal Price { get; set; }
	}
}
