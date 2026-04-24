using PriceNegotiationApp.Application.Products.Dtos;
using PriceNegotiationApp.Contracts.Products.Dtos;

namespace PriceNegotiationApp.Presentation.Products.Mappers
{
    public static class ProductViewModelMapperExtensions
    {
        extension(ProductViewModel productViewModel)
        {
            public ProductDto ToDto()
            {
                return new ProductDto(
                    productViewModel.Id,
                    productViewModel.Name,
                    productViewModel.Price);
            }
        }
    }
}
