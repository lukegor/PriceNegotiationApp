using Xunit;

namespace PriceNegotiationApp.IntegrationTests.Support;

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<IntegrationTestFixture>
{
    public const string Name = "api";
}
