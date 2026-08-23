namespace PriceNegotiationApp.IntegrationTests.Support;

public sealed class UserSession(IntegrationTestFactory factory, string email, string token)
{
    public string Email { get; } = email;

    public string Token { get; } = token;

    public HttpClient Client { get; } = factory.CreateDefaultClient(new BearerTokenHandler(new TokenHolder { Token = token }));
}
