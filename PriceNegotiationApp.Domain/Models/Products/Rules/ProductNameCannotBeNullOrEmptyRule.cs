using PriceNegotiationApp.Domain.Models.Abstract;

namespace PriceNegotiationApp.Domain.Models.Products.Rules
{
    internal class ProductNameCannotBeNullOrEmptyRule(string name) : IBusinessRule
    {
        public string Message => "Product name cannot be null or empty.";

        public bool IsBroken()
            => string.IsNullOrEmpty(name);
    }
}
