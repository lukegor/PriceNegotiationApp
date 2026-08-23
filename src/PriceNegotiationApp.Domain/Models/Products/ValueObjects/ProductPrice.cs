using PriceNegotiationApp.Domain.Models.Abstract;
using PriceNegotiationApp.Domain.Models.Products.Rules;

namespace PriceNegotiationApp.Domain.Models.Products.ValueObjects
{
    public class ProductPrice : ValueObject
    {
        public decimal Value { get; }

        /// <summary>
        /// Empty constructor for EF.
        /// </summary>
        private ProductPrice() { }

        public ProductPrice(decimal price)
        {
            CheckRule(new ProductPriceCannotBeNegativeOrZeroRule(price));

            Value = price;
        }
    }
}
