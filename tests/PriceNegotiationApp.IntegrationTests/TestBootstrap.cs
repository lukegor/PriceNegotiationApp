using PriceNegotiationApp.TestKit;
using System.Runtime.CompilerServices;
using Xunit;

namespace PriceNegotiationApp.IntegrationTests;

public static class TestBootstrap
{
    [ModuleInitializer]
    internal static void WireFuzzSink() =>
        Fuzz.Sink = line => TestContext.Current?.TestOutputHelper?.WriteLine(line);
}
