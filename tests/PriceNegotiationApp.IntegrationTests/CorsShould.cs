using PriceNegotiationApp.IntegrationTests.Support;
using Shouldly;
using System.Net;
using Xunit;

namespace PriceNegotiationApp.IntegrationTests;

[Collection(ApiCollection.Name)]
public class CorsShould(IntegrationTestFixture fixture)
{
    private const string AllowedOrigin = "https://app.test.local";

    [Fact]
    public async Task Configured_origin_receives_allow_origin_header()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/products");
        request.Headers.TryAddWithoutValidation("Origin", AllowedOrigin);

        var response = await fixture.Anonymous.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowed).ShouldBeTrue();
        allowed!.ShouldBe([AllowedOrigin]);
    }

    [Fact]
    public async Task Unlisted_origin_receives_no_allow_origin_header()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/products");
        request.Headers.TryAddWithoutValidation("Origin", "https://evil.example");

        var response = await fixture.Anonymous.SendAsync(request, TestContext.Current.CancellationToken);

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }
}
