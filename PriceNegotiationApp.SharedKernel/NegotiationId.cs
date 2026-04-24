using Vogen;

namespace PriceNegotiationApp.Domain.Models.Negotiations
{
    [ValueObject<Guid>(conversions: Conversions.EfCoreValueConverter)]
    public readonly partial record struct NegotiationId
    {
    }
}
