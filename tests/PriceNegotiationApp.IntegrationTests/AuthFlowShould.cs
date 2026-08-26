using PriceNegotiationApp.IntegrationTests.Support;
using PriceNegotiationApp.TestKit;
using Shouldly;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

        me.StatusCode.ShouldBe(HttpStatusCode.OK);
        var user = await me.Content.ReadFromJsonAsync<MeResponse>(TestContext.Current.CancellationToken);
        user!.Email.ShouldBe(session.Email);
        user.Roles.ShouldContain("Customer");
    }

    [Fact]
    public async Task Duplicate_registration_conflicts()
    {
        var email = Fuzz.UniqueEmail();
        var password = Fuzz.Password();
        var body = new RegisterRequest { Email = email, Password = password };

        var first = await fixture.Anonymous.PostAsJsonAsync("/api/v1/auth/register", body, TestContext.Current.CancellationToken);
        var second = await fixture.Anonymous.PostAsJsonAsync("/api/v1/auth/register", body, TestContext.Current.CancellationToken);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Invalid_registration_payload_is_unprocessable()
    {
        var response = await fixture.Anonymous.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest { Email = "not-an-email", Password = "short" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("registration_invalid");
    }

    [Fact]
    public async Task Bad_password_is_unauthorized_with_stable_code()
    {
        var session = await fixture.CreateUserAsync();

        var response = await fixture.Anonymous.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest { Email = session.Email, Password = "WrongPass1!" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("invalid_credentials");
    }

    [Fact]
    public async Task Locked_account_reports_invalid_credentials_like_any_failure()
    {
        var session = await fixture.CreateUserAsync();

        for (var i = 0; i < 5; i++)
        {
            await fixture.Anonymous.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = session.Email, Password = "WrongPass1!" }, TestContext.Current.CancellationToken);
        }

        // Even the correct password is now rejected because of the lockout
        var retry = await fixture.Anonymous.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest { Email = session.Email, Password = session.Password }, TestContext.Current.CancellationToken);

        retry.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var body = await retry.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        CodeOf(body).ShouldBe("invalid_credentials");
    }

    [Fact]
    public async Task Unknown_email_and_wrong_password_are_indistinguishable()
    {
        var session = await fixture.CreateUserAsync();

        var unknown = await fixture.Anonymous.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest { Email = Fuzz.UniqueEmail(), Password = "Whatever1!" }, TestContext.Current.CancellationToken);
        var wrongPassword = await fixture.Anonymous.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest { Email = session.Email, Password = "WrongPass1!" }, TestContext.Current.CancellationToken);

        unknown.StatusCode.ShouldBe(wrongPassword.StatusCode);
        CodeOf(await unknown.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .ShouldBe(CodeOf(await wrongPassword.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
    }

    private static string CodeOf(string problemDetails)
    {
        using var document = JsonDocument.Parse(problemDetails);
        return document.RootElement.GetProperty("code").GetString()!;
    }

    [Fact]
    public async Task Me_requires_authentication()
    {
        var response = await fixture.Anonymous.GetAsync("/api/v1/auth/me", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}

