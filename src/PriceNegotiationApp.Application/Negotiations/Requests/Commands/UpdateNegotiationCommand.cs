using PriceNegotiationApp.Domain.Models.Negotiations;
using PriceNegotiationApp.Domain.Models.Negotiations.ValueObjects;

namespace PriceNegotiationApp.Application.Negotiations.Requests.Commands
{
    public record UpdateNegotiationCommand(
        NegotiationId Id,
        ProposedPrice ProposedPrice);
}
