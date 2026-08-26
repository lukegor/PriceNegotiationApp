using PriceNegotiationApp.IntegrationTests.Support;
using Shouldly;
using System.Net;
using System.Text;
using Xunit;

namespace PriceNegotiationApp.IntegrationTests;

[Collection(ApiCollection.Name)]
public class PublicSurfaceShould(IntegrationTestFixture fixture)
{
    public static TheoryData<HttpMethod, string> ProtectedRoutes => new()
    {
        { HttpMethod.Get, "/api/v1/auth/me" },
        { HttpMethod.Post, "/api/v1/products" },
        { HttpMethod.Put, $"/api/v1/products/{Guid.NewGuid()}" },
        { HttpMethod.Delete, $"/api/v1/products/{Guid.NewGuid()}" },
        { HttpMethod.Post, "/api/v1/negotiations" },
        { HttpMethod.Get, "/api/v1/negotiations/mine" },
        { HttpMethod.Get, "/api/v1/negotiations" },
        { HttpMethod.Get, $"/api/v1/negotiations/{Guid.NewGuid()}" },
        { HttpMethod.Patch, $"/api/v1/negotiations/{Guid.NewGuid()}/proposals" },
        { HttpMethod.Post, $"/api/v1/negotiations/{Guid.NewGuid()}/accept" },
        { HttpMethod.Post, $"/api/v1/negotiations/{Guid.NewGuid()}/decline" },
        { HttpMethod.Delete, $"/api/v1/negotiations/{Guid.NewGuid()}" },
    };

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task Unauthenticated_requests_are_challenged(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json"),
        };

        var response = await fixture.Anonymous.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, $"{method} {path} must stay behind authentication");
    }
}
