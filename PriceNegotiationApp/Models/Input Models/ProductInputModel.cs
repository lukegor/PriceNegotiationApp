using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PriceNegotiationApp.Models.Input_Models
{
	public class ProductInputModel
	{
		[Required]
		public string Name { get; set; }
		[Required]

		[Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than or equal to 0.01")]
		public decimal Price { get; set; }


		public override bool Equals(object? obj)
		{
			if (obj == null || this.GetType() != obj.GetType())
				return false;

			return obj is ProductInputModel model &&
				   Name == model.Name &&
				   Price == model.Price;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(Name, Price);
		}
	}
}
