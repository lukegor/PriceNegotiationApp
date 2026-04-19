using FluentAssertions;
using System.Runtime.CompilerServices;

namespace PriceNegotiationApp.UnitTests
{
    internal static class TestConfig
    {
        [ModuleInitializer]
        public static void Initialize()
        {
            AssertionConfiguration.Current.Equivalency.Modify(options =>
                options.ThrowingOnMissingMembers());
        }
    }
}
