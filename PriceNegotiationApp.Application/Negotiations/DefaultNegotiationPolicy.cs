using PriceNegotiationApp.Domain.Models.Customer;
using PriceNegotiationApp.Domain.Models.Negotiations;
using PriceNegotiationApp.Domain.Models.Products;

namespace PriceNegotiationApp.Application.Negotiations
{
    public class DefaultNegotiationPolicy : INegotiationPolicy
    {
        public int CalculateRetries(CustomerId customerId, ProductId productId)
        {
            const int maxRetries = 3;
            return maxRetries;
        }

        public decimal CalculateMaxAllowedPrice(decimal initialPrice, ProductId productId)
        {
            const decimal multiplier = 2;
            return initialPrice * multiplier;
        }
    }
}
