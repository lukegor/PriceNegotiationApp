using System.Net;
using System.Net.Http.Json;
using PriceNegotiationApp.Api.Contracts;
using PriceNegotiationApp.IntegrationTests.Support;
using Xunit;

namespace PriceNegotiationApp.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AuthFlowShould(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task Register_login_and_read_current_user()
    {
        var session = await fixture.CreateUserAsync();

        var me = await session.Client.GetAsync("/api/v1/auth/me", TestContext.Current.CancellationToken);

        me.EnsureSuccessStatusCode();
        var user = await me.Content.ReadFromJsonAsync<MeResponse>(TestContext.Current.CancellationToken);
        Assert.Equal(session.Email, user!.Email);
        Assert.Contains("Customer", user.Roles);
    }

    [Fact]
    public async Task Duplicate_registration_conflicts()
    {
        var email = $"dup.{Guid.NewGuid():N}@test.local";
        var body = new RegisterRequest { Email = email, Password = "Passw0rd!x" };

        var first = await fixture.Anonymous.PostAsJsonAsync("/api/v1/auth/register", body, TestContext.Current.CancellationToken);
        var second = await fixture.Anonymous.PostAsJsonAsync("/api/v1/auth/register", body, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Bad_password_is_unauthorized_with_stable_code()
    {
        var session = await fixture.CreateUserAsync();

        var response = await fixture.Anonymous.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest { Email = session.Email, Password = "WrongPass1!" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("invalid_credentials", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Five_failed_attempts_lock_account()
    {
        var session = await fixture.CreateUserAsync();

        for (var i = 0; i < 5; i++)
        {
            await fixture.Anonymous.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = session.Email, Password = "WrongPass1!" }, TestContext.Current.CancellationToken);
        }

        // Even the correct password is now rejected because of the lockout
        var retry = await fixture.Anonymous.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest { Email = session.Email, Password = "Passw0rd!x" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, retry.StatusCode);
        var body = await retry.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("account_locked", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Me_requires_authentication()
    {
        var response = await fixture.Anonymous.GetAsync("/api/v1/auth/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

