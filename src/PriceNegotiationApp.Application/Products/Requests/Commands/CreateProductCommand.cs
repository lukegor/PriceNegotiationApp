using PriceNegotiationApp.Domain.Models.Products.ValueObjects;

namespace PriceNegotiationApp.Application.Products.Requests.Commands
{
    public record CreateProductCommand(
        string Name,
        ProductPrice Price);
}
