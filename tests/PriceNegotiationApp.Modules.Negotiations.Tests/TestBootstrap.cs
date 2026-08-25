using System.Runtime.CompilerServices;
using PriceNegotiationApp.TestKit;
using Xunit;

namespace PriceNegotiationApp.Modules.Negotiations.Tests;

public static class TestBootstrap
{
    [ModuleInitializer]
    internal static void WireFuzzSink() =>
        Fuzz.Sink = line => TestContext.Current?.TestOutputHelper?.WriteLine(line);
}
