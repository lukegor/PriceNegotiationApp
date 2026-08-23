using PriceNegotiationApp.Domain.Models.Abstract;
using PriceNegotiationApp.Domain.Models.Negotiations.Rules;

namespace PriceNegotiationApp.Domain.Models.Negotiations.ValueObjects
{
    public class ProposedPrice : ValueObject
    {
        public decimal Value { get; }

        /// <summary>
        /// Empty constructor for EF.
        /// </summary>
        private ProposedPrice() { }

        public ProposedPrice(decimal price)
        {
            CheckRule(new ProposedPriceCannotBeNegativeOrZeroRule(price));

            Value = price;
        }
    }
}
