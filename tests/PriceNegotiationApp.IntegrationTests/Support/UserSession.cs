namespace PriceNegotiationApp.IntegrationTests.Support;

public sealed class UserSession(
    IntegrationTestFactory factory, string email, string token, string password)
{
    public string Email { get; } = email;

    public string Token { get; } = token;

    public string Password { get; } = password;

    public HttpClient Client { get; } =
        factory.CreateDefaultClient(new BearerTokenHandler(new TokenHolder { Token = token }));
}
