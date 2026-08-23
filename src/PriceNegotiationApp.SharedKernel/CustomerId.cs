using Vogen;

namespace PriceNegotiationApp.Domain.Models.Customers
{
    [ValueObject<Guid>(conversions: Conversions.EfCoreValueConverter)]
    public readonly partial record struct CustomerId
    {
        public static bool operator <(CustomerId left, CustomerId right) => Comparer<CustomerId>.Default.Compare(left, right) < 0;
        public static bool operator <=(CustomerId left, CustomerId right) => Comparer<CustomerId>.Default.Compare(left, right) <= 0;
        public static bool operator >(CustomerId left, CustomerId right) => Comparer<CustomerId>.Default.Compare(left, right) > 0;
        public static bool operator >=(CustomerId left, CustomerId right) => Comparer<CustomerId>.Default.Compare(left, right) >= 0;
    }
}
