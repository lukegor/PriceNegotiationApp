using PriceNegotiationApp.Application.Products.Requests.Commands;
using PriceNegotiationApp.Contracts.Products.Dtos.Requests;
using PriceNegotiationApp.Domain.Models.Products;
using PriceNegotiationApp.Domain.Models.Products.ValueObjects;

namespace PriceNegotiationApp.Presentation.Products.Mappers
{
    public static class ProductRequestMapperExtensions
    {
        extension(ProductRequestDto request)
        {
            public UpdateProductCommand ToUpdateProductCommand(ProductId productId)
            {
                return new UpdateProductCommand(
                    productId,
                    request.Name,
                    new ProductPrice(request.Price));
            }

            public CreateProductCommand ToCreateProductCommand()
            {
                return new CreateProductCommand(
                    request.Name,
                    new ProductPrice(request.Price));
            }
        }
    }
}
