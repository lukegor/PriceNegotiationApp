using PriceNegotiationApp.Domain.Models.Negotiations.ValueObjects;
using PriceNegotiationApp.Domain.Models.Products;

namespace PriceNegotiationApp.Application.Negotiations.Requests.Commands
{
    public record CreateNegotiationCommand(
        ProductId ProductId,
        ProposedPrice ProposedPrice);
}
