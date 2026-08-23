using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using PriceNegotiationApp.Api.Contracts;
using System.Net.Http.Json;
using Testcontainers.PostgreSql;
using Xunit;

namespace PriceNegotiationApp.IntegrationTests.Support;

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<IntegrationTestFixture>
{
    public const string Name = "api";
}

public sealed class IntegrationTestFactory(string connectionString) : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@test.local";

    public const string StaffEmail = "staff@test.local";

    public const string SeedPassword = "Seed123!a";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:ConnectionString", connectionString);
        builder.UseSetting("Jwt:Issuer", "integration-tests");
        builder.UseSetting("Jwt:Audience", "integration-tests");
        builder.UseSetting("Jwt:SecretKey", new string('t', 64));
        builder.UseSetting("Jwt:ExpiryMinutes", "30");
        builder.UseSetting("Seeding:AdminEmail", AdminEmail);
        builder.UseSetting("Seeding:AdminPassword", SeedPassword);
        builder.UseSetting("Seeding:StaffEmail", StaffEmail);
        builder.UseSetting("Seeding:StaffPassword", SeedPassword);
        builder.UseSetting("Seeding:SeedSampleProducts", "true");
        builder.UseSetting("RateLimiting:AuthPermitLimit", "1000");
    }
}

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

public sealed class UserSession(IntegrationTestFactory factory, string email, string token)
{
    public string Email { get; } = email;

    public string Token { get; } = token;

    public HttpClient Client { get; } = factory.CreateDefaultClient(new BearerTokenHandler(new TokenHolder { Token = token }));
}
