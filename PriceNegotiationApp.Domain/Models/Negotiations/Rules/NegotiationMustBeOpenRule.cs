using PriceNegotiationApp.Domain.Models.Abstract;
using PriceNegotiationApp.Domain.Models.Negotiations;

namespace PriceNegotiationApp.Domain.Models.Negotiations.Rules
{
    public class NegotiationMustBeOpenRule(NegotiationStatus status) : IBusinessRule
    {
        public string Message => "Negotiation must be open.";

        public bool IsBroken()
        {
            return status != NegotiationStatus.Open;
        }
    }
}
