using PriceNegotiationApp.Domain.Models.Abstract;

namespace PriceNegotiationApp.Domain.Models.Negotiations.Rules
{
    public class RetriesLeftMustBePositiveRule(int retriesLeft) : IBusinessRule
    {
        public string Message => "No retries left for this negotiation.";
        public bool IsBroken() => retriesLeft <= 0;
    }
}
