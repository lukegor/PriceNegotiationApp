using PriceNegotiationApp.Domain.Models.Products;

namespace PriceNegotiationApp.Application.Products.Requests.Queries
{
    public record GetProductByIdQuery(
        ProductId Id);
}
