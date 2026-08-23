using Vogen;

namespace PriceNegotiationApp.Domain.Models.Negotiations
{
    [ValueObject<Guid>(conversions: Conversions.EfCoreValueConverter)]
    public readonly partial record struct NegotiationId
    {
        public static bool operator <(NegotiationId left, NegotiationId right) => Comparer<NegotiationId>.Default.Compare(left, right) < 0;
        public static bool operator <=(NegotiationId left, NegotiationId right) => Comparer<NegotiationId>.Default.Compare(left, right) <= 0;
        public static bool operator >(NegotiationId left, NegotiationId right) => Comparer<NegotiationId>.Default.Compare(left, right) > 0;
        public static bool operator >=(NegotiationId left, NegotiationId right) => Comparer<NegotiationId>.Default.Compare(left, right) >= 0;
    }
}
