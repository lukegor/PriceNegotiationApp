using PriceNegotiationApp.Contracts.Products.Dtos;
using PriceNegotiationApp.Contracts.Products.Dtos.Responses;
using PriceNegotiationApp.Domain.Models.Products;

namespace PriceNegotiationApp.Presentation.Products.Mappers
{
    public static class ProductMappersExtensions
    {
        extension(Product product)
        {
            public ProductResponseDto ToResponseDto()
            {
                return new ProductResponseDto(
                    product.Id.Value,
                    product.Name,
                    product.Price.Value);
            }

            public ProductDto ToODataResponseDto()
            {
                return new ProductDto
                {
                    Id = product.Id.Value,
                    Name = product.Name,
                    Price = product.Price.Value
                };
            }
        }
    }
}
