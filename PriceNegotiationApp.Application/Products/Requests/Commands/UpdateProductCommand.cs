using PriceNegotiationApp.Domain.Models.Products;
using PriceNegotiationApp.Domain.Models.Products.ValueObjects;

namespace PriceNegotiationApp.Application.Products.Requests.Commands
{
    public record UpdateProductCommand(
        ProductId Id,
        string Name,
        ProductPrice Price);
}
