using PriceNegotiationApp.Domain.Models.Negotiations;
using PriceNegotiationApp.Domain.Models.Negotiations.ValueObjects;

namespace PriceNegotiationApp.Application.Negotiations.Requests.Commands
{
    public class ProposeNewPriceCommand
    {
        public NegotiationId NegotiationId { get; init; }
        public ProposedPrice ProposedPrice { get; init; }

        public ProposeNewPriceCommand(NegotiationId negotiationId, ProposedPrice proposedPrice)
        {
            NegotiationId = negotiationId;
            ProposedPrice = proposedPrice;
        }
    }
}
