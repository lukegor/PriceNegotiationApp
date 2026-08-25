using PriceNegotiationApp.TestKit;
using System.Runtime.CompilerServices;
using Xunit;

namespace PriceNegotiationApp.Modules.Identity.Tests;

public static class TestBootstrap
{
    [ModuleInitializer]
    internal static void WireFuzzSink() =>
        Fuzz.Sink = line => TestContext.Current?.TestOutputHelper?.WriteLine(line);
}
