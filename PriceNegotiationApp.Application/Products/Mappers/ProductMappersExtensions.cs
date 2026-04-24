using PriceNegotiationApp.Application.Products.Dtos;
using PriceNegotiationApp.Domain.Models.Products;

namespace PriceNegotiationApp.Application.Products.Mappers
{
    public static class ProductMappersExtensions
    {
        extension(Product product)
        {
            public ProductResultDto ToResultDto()
            {
                return new ProductResultDto(
                    product.Id.Value,
                    product.Name,
                    product.Price.Value);
            }

            public ProductViewModel ToViewModel()
            {
                return new ProductViewModel(
                    product.Id.Value,
                    product.Name,
                    product.Price.Value);
            }
        }
    }
}
