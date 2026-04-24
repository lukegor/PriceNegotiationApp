using PriceNegotiationApp.Contracts.Products.Dtos;
using PriceNegotiationApp.Contracts.Products.Dtos.Requests;
using PriceNegotiationApp.Domain.Models.Products;

namespace PriceNegotiationApp.IntegrationTests.Products
{
    public static class ProductMappingExtensions
    {
        public static ProductDto ToExpectedDto(this Product p)
        {
            return new ProductDto(
                p.Id.Value,
                p.Name,
                p.Price.Value
            );
        }

        public static ExpectedProductModel ToExpectedDto(this ProductRequestDto createDto)
        {
            return new ExpectedProductModel
            {
                Name = createDto.Name,
                Price = createDto.Price
            };
        }
    }
}
