using Vogen;

namespace PriceNegotiationApp.Domain.Models.Products
{
    [ValueObject<Guid>(conversions: Conversions.EfCoreValueConverter)]
    public readonly partial record struct ProductId
    {
    }
}
