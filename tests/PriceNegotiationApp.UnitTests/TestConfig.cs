using FluentAssertions;
using System.Runtime.CompilerServices;

namespace PriceNegotiationApp.UnitTests
{
    internal static class TestConfig
    {
        [ModuleInitializer]
        public static void Initialize()
        {
            License.Accepted = true;
            AssertionConfiguration.Current.Equivalency.Modify(options =>
                options.ThrowingOnMissingMembers());
        }
    }
}
