using PriceNegotiationApp.Domain.Models.Abstract;

namespace PriceNegotiationApp.Domain.Models.Products.Rules
{
    public class ProductPriceCannotBeNegativeOrZeroRule(decimal price) : IBusinessRule
    {
        public string Message => "Products price cannot be negative or zero.";

        public bool IsBroken()
        {
            return price <= 0;
        }
    }
}
