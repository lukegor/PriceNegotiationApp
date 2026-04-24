using PriceNegotiationApp.Domain.Models.Customer;
using PriceNegotiationApp.Domain.Models.Products;

namespace PriceNegotiationApp.Domain.Models.Negotiations
{
    public interface INegotiationPolicy
    {
        int CalculateRetries(CustomerId customerId, ProductId productId);
        decimal CalculateMaxAllowedPrice(decimal initialPrice, ProductId productId);
    }
}
