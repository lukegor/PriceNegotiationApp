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
		[Required]

		[Range(0, double.MaxValue, ErrorMessage = "Price must be greater than or equal to 0")]
		public decimal Price { get; set; }


		public override bool Equals(object obj)
		{
			// Step 1: Check for null and type matching
			if (obj == null || this.GetType() != obj.GetType())
			{
				return false;
			}

			// Step 2: Cast the object to the correct type
			var other = (Product)obj;

			// Step 3: Compare individual properties
			// Note: You can customize this comparison based on your requirements
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
