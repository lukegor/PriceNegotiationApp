using PriceNegotiationApp.IntegrationTests.Support;
using Shouldly;
using System.Net;
using System.Text.Json;
using Xunit;

namespace PriceNegotiationApp.IntegrationTests;

[Collection(ApiCollection.Name)]
public class JwksShould(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task Publish_only_public_material_matching_issued_tokens()
    {
        var session = await fixture.CreateUserAsync();

        var header = DecodeJson(session.Token.Split('.')[0]);
        header.GetProperty("alg").GetString().ShouldBe("ES256");
        var kid = header.GetProperty("kid").GetString();
        kid.ShouldNotBeNullOrEmpty();

        var response = await fixture.Anonymous.GetAsync("/.well-known/jwks.json", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain(kid);
        body.ShouldContain("\"crv\":\"P-256\"");
        body.ShouldNotContain("\"d\"");
        body.ShouldNotContain("PRIVATE");
    }

    private static JsonElement DecodeJson(string base64Url)
    {
        var padded = base64Url.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
        }

        return JsonSerializer.Deserialize<JsonElement>(Convert.FromBase64String(padded));
    }
}
