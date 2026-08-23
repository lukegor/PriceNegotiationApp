using Vogen;

namespace PriceNegotiationApp.Domain.Models.Products
{
    [ValueObject<Guid>(conversions: Conversions.EfCoreValueConverter)]
    public readonly partial record struct ProductId
    {
        public static bool operator <(ProductId left, ProductId right) => Comparer<ProductId>.Default.Compare(left, right) < 0;
        public static bool operator <=(ProductId left, ProductId right) => Comparer<ProductId>.Default.Compare(left, right) <= 0;
        public static bool operator >(ProductId left, ProductId right) => Comparer<ProductId>.Default.Compare(left, right) > 0;
        public static bool operator >=(ProductId left, ProductId right) => Comparer<ProductId>.Default.Compare(left, right) >= 0;
    }
}
