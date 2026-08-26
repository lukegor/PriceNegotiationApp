using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PriceNegotiationApp.IntegrationTests.Support;

public sealed class IntegrationTestFactory(string connectionString) : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@test.local";

    public const string StaffEmail = "staff@test.local";

    public const string SeedPassword = "Seed123!Apricot!";

    private static readonly string SigningPem = CreateSigningPem();

    private static string CreateSigningPem()
    {
        using var ecdsa = System.Security.Cryptography.ECDsa.Create(
            System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        return ecdsa.ExportPkcs8PrivateKeyPem();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:ConnectionString", connectionString);
        builder.UseSetting("Jwt:Issuer", "integration-tests");
        builder.UseSetting("Jwt:Audience", "integration-tests");
        builder.UseSetting("Jwt:PrivateKey", SigningPem);
        builder.UseSetting("Jwt:ExpiryMinutes", "30");
        builder.UseSetting("Seeding:AdminEmail", AdminEmail);
        builder.UseSetting("Seeding:AdminPassword", SeedPassword);
        builder.UseSetting("Seeding:StaffEmail", StaffEmail);
        builder.UseSetting("Seeding:StaffPassword", SeedPassword);
        builder.UseSetting("Seeding:SeedSampleProducts", "true");
        builder.UseSetting("RateLimiting:AuthPermitLimit", "1000");
    }
}
