using PriceNegotiationApp.Api.Contracts;
using System.Net.Http.Json;
using Testcontainers.PostgreSql;
using Xunit;

namespace PriceNegotiationApp.IntegrationTests.Support;

public sealed class IntegrationTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public IntegrationTestFactory Factory { get; private set; } = null!;

    public HttpClient Anonymous { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        Factory = new IntegrationTestFactory(_postgres.GetConnectionString());
        Anonymous = Factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        Anonymous.Dispose();
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>Registers and logs in a fresh customer; returns an authorized client session.</summary>
    public async Task<UserSession> CreateUserAsync()
    {
        var email = $"customer.{Guid.NewGuid():N}@test.local";
        var password = "Passw0rd!x";

        var register = await Anonymous.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest { Email = email, Password = password });
        register.EnsureSuccessStatusCode();

        return await LoginAsync(email, password);
    }

    public async Task<UserSession> LoginAsync(string email, string password)
    {
        var login = await Anonymous.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest { Email = email, Password = password });
        login.EnsureSuccessStatusCode();
        var content = await login.Content.ReadFromJsonAsync<LoginResponse>();
        return new UserSession(Factory, email, content!.AccessToken);
    }

    public Task<UserSession> LoginAsAdminAsync() =>
        LoginAsync(IntegrationTestFactory.AdminEmail, IntegrationTestFactory.SeedPassword);

    public Task<UserSession> LoginAsStaffAsync() =>
        LoginAsync(IntegrationTestFactory.StaffEmail, IntegrationTestFactory.SeedPassword);
}
