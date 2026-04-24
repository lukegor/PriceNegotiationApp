using FluentAssertions;
using System.Runtime.CompilerServices;

namespace PriceNegotiationApp.UnitTests
{
    /// <summary>
    /// Global configuration for FluentAssertions.
    /// </summary>
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
