using PriceNegotiationApp.Domain.Models.Products.Dto;
using PriceNegotiationApp.Domain.Models.Products;
using PriceNegotiationApp.Domain.Models.Products.ValueObjects;

namespace PriceNegotiationApp.Domain.Models.Mappers
{
	public static class CreateProductRequestMapperExtension
	{
		public static Product ToProduct(this ProductRequestDto product)
		{
			return new Product(
				product.Name,
				new ProductPrice(product.Price)
			);
		}
	}
}
