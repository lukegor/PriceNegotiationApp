using PriceNegotiationApp.Application.Products.Dtos;
using PriceNegotiationApp.Domain.Models.Products;
using System.Linq.Expressions;

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

            public static Expression<Func<Product, ProductViewModel>> ToViewModel()
            {
                return product => new ProductViewModel(
                    product.Id.Value,
                    product.Name,
                    product.Price.Value);
            }
        }
    }
}
