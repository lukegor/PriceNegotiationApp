using PriceNegotiationApp.Application.Products.Dtos;
using PriceNegotiationApp.Contracts.Products.Dtos.Responses;

namespace PriceNegotiationApp.Presentation.Products.Mappers
{
    public static class ProductResultDtoMappersExtensions
    {
        extension(ProductResultDto product)
        {
            public ProductResponseDto ToResponseDto()
            {
                return new ProductResponseDto(
                    product.Id,
                    product.Name,
                    product.Price);
            }
        }
    }
}
