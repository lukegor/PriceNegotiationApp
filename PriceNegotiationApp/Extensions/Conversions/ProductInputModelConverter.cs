using PriceNegotiationApp.Models;
using PriceNegotiationApp.Models.Input_Models;

namespace PriceNegotiationApp.Extensions.Conversions
{
	public static class ProductInputModelConverter
	{
		public static Product ToDb(this ProductInputModel product)
		{
			return new Product
			{
				Name = product.Name,
				Price = product.Price
			};
		}
	}
}
