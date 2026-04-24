using Vogen;

namespace PriceNegotiationApp.Domain.Models.Customer
{
    [ValueObject<Guid>(conversions: Conversions.EfCoreValueConverter)]
    public readonly partial record struct CustomerId
    {
    }
}
