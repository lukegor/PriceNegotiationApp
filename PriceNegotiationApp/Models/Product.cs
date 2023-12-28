using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PriceNegotiationApp.Models
{
	public class Product
	{
		[Key]
		//[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public string Id { get; set; } = Guid.NewGuid().ToString();
		[Required]
		public string Name { get; set; }
		[Required]
		[Range(0, double.MaxValue, ErrorMessage = "Price must be greater than or equal to 0")]
		public decimal Price { get; set; }


		public override bool Equals(object obj)
		{
			// 1
			if (obj == null || this.GetType() != obj.GetType())
			{
				return false;
			}

			// 2
			var other = (Product)obj;

			// 3
			return Id == other.Id && Name == other.Name && Price == other.Price;
		}

		// Step 4: Override GetHashCode for consistency (required when overriding Equals)
		public override int GetHashCode()
		{
			// Combine hash codes of individual properties
			return HashCode.Combine(Id, Name, Price);
		}
	}
}
