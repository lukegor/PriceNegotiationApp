using PriceNegotiationApp.Domain.Models.Abstract;

namespace PriceNegotiationApp.Domain.Models.Negotiations.Rules
{
    public class ProposedPriceCannotBeNegativeOrZeroRule(decimal price) : IBusinessRule
    {
        public string Message => "Proposed price cannot be negative or zero.";

        public bool IsBroken()
        {
            return price <= 0;
        }
    }
}
