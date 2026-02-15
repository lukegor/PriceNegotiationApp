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
                return new ProductDto
                {
                    Id = productViewModel.Id,
                    Name = productViewModel.Name,
                    Price = productViewModel.Price
                };
            }
        }
    }
}
