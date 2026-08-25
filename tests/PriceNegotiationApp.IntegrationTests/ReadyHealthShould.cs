using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PriceNegotiationApp.IntegrationTests.Support;
using Shouldly;
using Xunit;

namespace PriceNegotiationApp.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ReadyHealthShould(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task Ready_reports_json_status_per_dependency()
    {
        var response = await fixture.Anonymous.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            cancellationToken: TestContext.Current.CancellationToken);

        body.GetProperty("status").GetString().ShouldBe("Healthy");
        body.GetProperty("totalDurationMs").GetDouble().ShouldBeGreaterThanOrEqualTo(0);

        var entries = body.GetProperty("entries");
        foreach (var name in new[] { "database-identity", "database-catalog", "database-negotiations" })
        {
            entries.TryGetProperty(name, out _).ShouldBeTrue($"missing health entry '{name}'");
            entries.GetProperty(name).GetProperty("status").GetString().ShouldBe("Healthy");
            entries.GetProperty(name).TryGetProperty("description", out _)
                .ShouldBeFalse("healthy checks must not carry a description");
        }
    }
}
