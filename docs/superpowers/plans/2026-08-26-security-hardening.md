# Security Review Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the confirmed security findings from the 2026-08-26 review: readiness-body info leak, login account-enumeration oracle, weak seed credentials, duplicated JWT config, and symmetric HMAC signing (migrate to ES256 + JWKS).

**Architecture:** All fixes ride existing seams — option validators (`IValidateOptions` + `ValidateOnStart`), handler-owned application logic behind transport-only endpoints, and the WebApplicationFactory/Testcontainers integration harness. The ES256 migration introduces one singleton (`EcSigningKey`) in the Identity module's `Features/Auth`; the composition root consumes its public half for bearer validation and publishes JWKS.

**Tech Stack:** .NET 10 / ASP.NET Core minimal APIs, ASP.NET Core Identity, `System.IdentityModel.Tokens.Jwt` 8.19.2 (brings `Microsoft.IdentityModel.Tokens` with `JsonWebKey`, `JsonWebKeyConverter`, `ComputeJwkThumbprint`), xUnit v3 + Shouldly, Testcontainers PostgreSQL.

**Spec:** `docs/superpowers/specs/2026-08-26-security-review-design.md`

## Global Constraints

- `TreatWarningsAsErrors=true` and analyzers run on every project — zero warnings allowed.
- Tactical DDD laws (enforced by ArchUnitNET): endpoints are transport-only; handlers own persistence; no repository abstractions.
- Stable machine-readable error `code` extensions on every ProblemDetails response.
- No secrets committed: user-secrets locally, environment variables in compose. Placeholder values in `.env.example` must **fail** startup validation if deployed verbatim.
- Comments follow repo style: short rationale comments allowed where non-obvious (see existing files).
- Integration tests require Docker (Testcontainers). Unit facts live in the same projects without `[Collection]` attributes and skip Docker.
- Full-suite gate before final commit of each task: `dotnet test --project <changed test project>` per task; whole solution green at Task 6.

---

### Task 1: Sanitize `/health/ready` response body (spec F1)

The readiness endpoint is anonymous and currently returns `description = exception.Message` for unhealthy checks, leaking dependency internals. Detail moves to server logs only.

**Files:**
- Modify: `src/PriceNegotiationApp.Api/ReadyHealthReport.cs`
- Create: `tests/PriceNegotiationApp.IntegrationTests/ReadyHealthReportShould.cs`
- Modify (no-op verification): `tests/PriceNegotiationApp.IntegrationTests/ReadyHealthShould.cs` (existing assertions must stay green)

**Interfaces:**
- Consumes: framework `HealthReport` / `HealthReportEntry`.
- Produces: same static signature `ReadyHealthReport.WriteAsync(HttpContext, HealthReport)`; body entries now always `{ status, durationMs }`.

- [ ] **Step 1: Rewrite the response writer**

Replace the whole content of `src/PriceNegotiationApp.Api/ReadyHealthReport.cs`:

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace PriceNegotiationApp.Api;

/// <summary>
/// JSON body for /health/ready naming every dependency and its verdict.
/// Failure detail goes to logs only; the anonymous body stays free of it.
/// </summary>
public static class ReadyHealthReport
{
    public static async Task WriteAsync(HttpContext context, HealthReport report)
    {
        var logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(ReadyHealthReport));

        foreach (var (name, entry) in report.Entries.Where(e => e.Value.Status != HealthStatus.Healthy))
        {
            logger.LogWarning("Readiness check '{Check}' is unhealthy: {Detail}",
                name, entry.Value.Description ?? entry.Value.Exception?.Message);
        }

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            entries = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString(),
                    durationMs = entry.Value.Duration.TotalMilliseconds,
                }),
        };

        await context.Response.WriteAsJsonAsync(payload);
    }
}
```

- [ ] **Step 2: Add the leak-regression unit test**

Create `tests/PriceNegotiationApp.IntegrationTests/ReadyHealthReportShould.cs`:

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PriceNegotiationApp.Api;
using Shouldly;
using Xunit;

namespace PriceNegotiationApp.IntegrationTests;

// Plain unit fact over the response writer; no Docker container required.
public class ReadyHealthReportShould
{
    [Fact]
    public async Task Never_leak_failure_detail_in_body_even_when_unhealthy()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var secret = "password authentication failed for user 'postgres'";
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["database-catalog"] = new(
                    HealthStatus.Unhealthy, secret, TimeSpan.FromMilliseconds(3),
                    new InvalidOperationException(secret), null),
                ["self"] = new(
                    HealthStatus.Healthy, null, TimeSpan.FromMilliseconds(1), null, null),
            },
            totalDuration: TimeSpan.FromMilliseconds(4));

        await ReadyHealthReport.WriteAsync(context, report);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        body.ShouldNotContain(secret);
        body.ShouldNotContain("description");
        body.ShouldContain("\"Unhealthy\"");
    }
}
```

- [ ] **Step 3: Run validation**

```bash
dotnet test --project tests/PriceNegotiationApp.IntegrationTests --filter ReadyHealth
```

Both `ReadyHealthShould` (Docker) and `ReadyHealthReportShould` (plain) must pass.

- [ ] **Step 4: Commit**

```bash
git add src/PriceNegotiationApp.Api/ReadyHealthReport.cs tests/PriceNegotiationApp.IntegrationTests/ReadyHealthReportShould.cs
git commit -m "fix(health): keep readiness body free of dependency failure detail"
```

---

### Task 2: Uniform login failures (spec F2)

Locked accounts currently answer `account_locked` while everything else answers `invalid_credentials` — an enumeration oracle. All authentication failures become indistinguishable externally; lockout mechanics stay intact internally.

**Files:**
- Modify: `src/PriceNegotiationApp.Modules.Identity/Features/Auth/LoginUserHandler.cs`
- Modify: `src/PriceNegotiationApp.Modules.Identity/Public/IdentityErrorCodes.cs` (remove `AccountLocked`)
- Modify: `tests/PriceNegotiationApp.IntegrationTests/AuthFlowShould.cs`

**Interfaces:**
- Produces: every failed login → HTTP 401, `code: "invalid_credentials"`.

- [ ] **Step 1: Make the handler uniform**

Replace the content of `LoginUserHandler.HandleAsync` and add the private factory (class declaration/constructor unchanged):

```csharp
    public async Task<AuthResponse> HandleAsync(LoginRequest request)
    {
        var user = await userManager.FindByNameAsync(request.Email)
                   ?? throw Unauthorized();

        // Lockout keeps enforcing internally but reads identically to any other failure.
        if (await userManager.IsLockedOutAsync(user))
        {
            throw Unauthorized();
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            throw Unauthorized();
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var roles = (IReadOnlyList<string>)await userManager.GetRolesAsync(user);
        var (token, expiresAtUtc) = jwt.Generate(user.Id, request.Email, roles);
        return new AuthResponse(token, expiresAtUtc, request.Email, roles);
    }

    private static UnauthorizedException Unauthorized() =>
        new(IdentityErrorCodes.InvalidCredentials, "Invalid credentials.");
```

- [ ] **Step 2: Remove the dead error code**

In `src/PriceNegotiationApp.Modules.Identity/Public/IdentityErrorCodes.cs`, delete the line:

```csharp
    public const string AccountLocked = "account_locked";
```

- [ ] **Step 3: Update and extend the integration tests**

In `tests/PriceNegotiationApp.IntegrationTests/AuthFlowShould.cs`:

Rename `Five_failed_attempts_lock_account` and change its final assertion:

```csharp
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
```

Add this test and helper to the same class (add `using System.Text.Json;` to the file):

```csharp
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
```

Note: `Bad_password_is_unauthorized_with_stable_code` already expects `invalid_credentials` and must stay green untouched.

- [ ] **Step 4: Run validation**

```bash
dotnet test --project tests/PriceNegotiationApp.IntegrationTests --filter AuthFlow
```

- [ ] **Step 5: Commit**

```bash
git add src/PriceNegotiationApp.Modules.Identity tests/PriceNegotiationApp.IntegrationTests/AuthFlowShould.cs
git commit -m "fix(identity): answer every failed login with invalid_credentials"
```

---

### Task 3: Strong seed credentials + loud seeding failures (spec F3)

Seed passwords were floored at 8 chars and examples shipped `Admin123!`. Raise the floor to mirror what ASP.NET Identity actually accepts, and stop swallowing silent seed-user creation failures.

**Files:**
- Modify: `src/PriceNegotiationApp.Modules.Identity/Seeding/SeedingOptionsValidator.cs`
- Modify: `src/PriceNegotiationApp.Modules.Identity/Seeding/IdentitySeedingHostedService.cs`
- Modify: `tests/PriceNegotiationApp.Modules.Identity.Tests/SeedingOptionsValidatorShould.cs`
- Modify: `tests/PriceNegotiationApp.IntegrationTests/Support/IntegrationTestFactory.cs:12` (`SeedPassword`)
- Modify: `.env.example`, `README.md:107-115` (quickstart secrets)

**Interfaces:**
- Produces: `Seeding:{Admin,Staff}Password` accepted iff ≥12 chars with upper-case, lower-case, digit, and symbol. Startup otherwise fails fast with that exact requirement text.

- [ ] **Step 1: Harden the validator**

In `SeedingOptionsValidator.cs`, replace both password blocks and add the helper (email logic untouched):

```csharp
        if (string.IsNullOrWhiteSpace(options.AdminPassword) || !IsStrong(options.AdminPassword))
        {
            failures.Add("Seeding:AdminPassword must be at least 12 characters and mix upper-case, lower-case, digit and symbol characters.");
        }

        if (string.IsNullOrWhiteSpace(options.StaffPassword) || !IsStrong(options.StaffPassword))
        {
            failures.Add("Seeding:StaffPassword must be at least 12 characters and mix upper-case, lower-case, digit and symbol characters.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }

    private static bool IsStrong(string password) =>
        password.Length >= 12
        && password.Any(char.IsUpper)
        && password.Any(char.IsLower)
        && password.Any(char.IsDigit)
        && password.Any(c => !char.IsLetterOrDigit(c));
```

- [ ] **Step 2: Log seed-user creation failures instead of skipping silently**

In `IdentitySeedingHostedService.cs`, replace `EnsureUserAsync` and its call sites' logging plumbing (the class already receives `ILogger<IdentitySeedingHostedService> logger` via its primary constructor):

```csharp
    protected override async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { UserRoles.Admin, UserRoles.Staff, UserRoles.Customer })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        await EnsureUserAsync(userManager, options.Value.AdminEmail, options.Value.AdminPassword, UserRoles.Admin);
        await EnsureUserAsync(userManager, options.Value.StaffEmail, options.Value.StaffPassword, UserRoles.Staff);
        logger.LogInformation("Identity seed data ensured.");
    }

    private async Task EnsureUserAsync(UserManager<ApplicationUser> userManager, string email, string password, string role)
    {
        if (string.IsNullOrWhiteSpace(password)
            || await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var user = new ApplicationUser { UserName = email, Email = email };
        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(user, role);
        }
        else
        {
            logger.LogError("Seeded user {Email} could not be created: {Errors}",
                email, string.Join("; ", result.Errors.Select(e => $"{e.Code} {e.Description}")));
        }
    }
```

(`EnsureUserAsync` drops `static` because it now uses `logger`.)

- [ ] **Step 3: Extend the validator tests**

In `SeedingOptionsValidatorShould.cs`, replace the weak-password theory with:

```csharp
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("short")]
    [InlineData("alllowercase123!")]
    [InlineData("ALLUPPERCASE123!")]
    [InlineData("NoDigitsHereOnly!!")]
    [InlineData("NoSymbols12345xY")]
    public void Reject_admin_password_below_strength_floor(string password)
    {
        var options = new SeedingOptions
        {
            AdminEmail = Fuzz.Email(),
            AdminPassword = password,
            StaffEmail = Fuzz.Email(),
            StaffPassword = Fuzz.Password(),
        };

        _sut.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Seed123!Apricot!")]
    [InlineData("Str0ng-Passphrase!42")]
    public void Accept_strong_admin_passwords(string password)
    {
        var options = new SeedingOptions
        {
            AdminEmail = Fuzz.Email(),
            AdminPassword = password,
            StaffEmail = Fuzz.Email(),
            StaffPassword = Fuzz.Password(),
        };

        _sut.Validate(null, options).Succeeded.ShouldBeTrue();
    }
```

(The existing `Aggregate_every_violation_in_one_result` expectation of exactly 2 failures still holds — blank passwords produce one failure line each.)

- [ ] **Step 4: Bump the integration fixture password**

In `IntegrationTestFactory.cs`:

```csharp
    public const string SeedPassword = "Seed123!Apricot!";
```

Then search for other literal uses of the old value: `rg "Seed123!a"` — every hit must be switched to the constant or the new value.

- [ ] **Step 5: Churn the shipped examples**

`.env.example` — replace the seed lines:

```
SEED_ADMIN_PASSWORD=replace-me-strong-random-Aa1!
SEED_STAFF_PASSWORD=replace-me-strong-random-Bb2!
```

`README.md` quickstart user-secrets block — replace the two seeding lines with:

```bash
dotnet user-secrets set "Seeding:AdminPassword" "<your own random 12+ char mixed secret>" --project src/PriceNegotiationApp.Api
dotnet user-secrets set "Seeding:StaffPassword" "<your own random 12+ char mixed secret>" --project src/PriceNegotiationApp.Api
```

- [ ] **Step 6: Run validation**

```bash
dotnet test --project tests/PriceNegotiationApp.Modules.Identity.Tests
dotnet test --project tests/PriceNegotiationApp.IntegrationTests --filter "FullyQualifiedName~ConfigurationValidation|FullyQualifiedName~AuthFlow"
```

- [ ] **Step 7: Commit**

```bash
git add src/PriceNegotiationApp.Modules.Identity tests .env.example README.md
git commit -m "feat(seeding): enforce strong seed passwords and log creation failures"
```

---

### Task 4: One validated JWT options contract (spec F5)

The Api duplicates the Identity module's `JwtOptions` as `JwtSettings` bound straight from configuration — bypassing validation. Delete the duplicate and configure bearer options through the DI options pattern against the module's validated `JwtOptions`.

**Files:**
- Modify: `src/PriceNegotiationApp.Api/Extensions/WebApplicationBuilderExtensions.cs:54-70`
- Delete: `src/PriceNegotiationApp.Api/Extensions/JwtSettings.cs`

**Interfaces:**
- Consumes: `PriceNegotiationApp.Modules.Identity.Features.Auth.JwtOptions` (validated, registered by `AddIdentityModule`).
- Produces: `JwtBearerOptions` for scheme `JwtBearerDefaults.AuthenticationScheme`, populated via `OptionsBuilder.Configure<IOptions<JwtOptions>>`.

- [ ] **Step 1: Rewire bearer setup**

Replace the authentication registration block in `WebApplicationBuilderExtensions.AddApiServices`:

```csharp
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Value.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Value.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Value.SecretKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                });
```

Add `using PriceNegotiationApp.Modules.Identity.Features.Auth;`. Delete `src/PriceNegotiationApp.Api/Extensions/JwtSettings.cs`, then remove now-unused usings from `WebApplicationBuilderExtensions.cs` (`System.Text` becomes unused in Task 5; leave it if still referenced).

- [ ] **Step 2: Run validation**

```bash
dotnet build -c Release
dotnet test --project tests/PriceNegotiationApp.IntegrationTests --filter AuthFlow
```

Behavior is unchanged; the suite passing proves the rewire.

- [ ] **Step 3: Commit**

```bash
git add -A src/PriceNegotiationApp.Api tests/PriceNegotiationApp.IntegrationTests
git commit -m "refactor(auth): validate bearer settings through the module-owned JwtOptions"
```

---

### Task 5: ES256 asymmetric signing + JWKS publication (spec F7)

Replace the shared HMAC secret with an EC P-256 key pair. Private half signs; public half (published at `/.well-known/jwks.json` with an RFC 7638 thumbprint `kid`) validates — future resource servers or extracted identity services never hold signing material.

**Files:**
- Create: `src/PriceNegotiationApp.Modules.Identity/Features/Auth/EcSigningKey.cs`
- Modify: `src/PriceNegotiationApp.Modules.Identity/Features/Auth/JwtOptions.cs`
- Modify: `src/PriceNegotiationApp.Modules.Identity/Features/Auth/JwtOptionsValidator.cs`
- Modify: `src/PriceNegotiationApp.Modules.Identity/Features/Auth/JwtManager.cs`
- Modify: `src/PriceNegotiationApp.Modules.Identity/IdentityModule.cs:41`
- Modify: `src/PriceNegotiationApp.Api/Extensions/WebApplicationBuilderExtensions.cs`
- Modify: `src/PriceNegotiationApp.Api/Extensions/PipelineExtensions.cs`
- Modify: `src/PriceNegotiationApp.Api/appsettings.json:15`
- Modify: `docker-compose.yml:12`, `.env.example:3`, `README.md:107-115` + config table row
- Modify: `tests/PriceNegotiationApp.IntegrationTests/Support/IntegrationTestFactory.cs:20`
- Test: rewrite `tests/PriceNegotiationApp.Modules.Identity.Tests/JwtManagerShould.cs`
- Create: `tests/PriceNegotiationApp.Modules.Identity.Tests/JwtOptionsValidatorShould.cs`
- Create: `tests/PriceNegotiationApp.IntegrationTests/JwksShould.cs`

**Interfaces:**
- Consumes: `JwtOptions.PrivateKey` (PKCS#8 PEM; literal newlines or `\n`-escaped).
- Produces:
  - `EcSigningKey` — singleton; `JsonWebKey PublicJwk` (public-only, `Kid` set), `string Kid`, `ECDsa CreatePrivateEcdsa()`, `const string Algorithm = SecurityAlgorithms.EcdSa256`.
  - JWKS endpoint `GET /.well-known/jwks.json` → `{ "keys": [ { kty, crv, x, y, kid } ] }`.
  - Config surface renamed: `Jwt:SecretKey` → `Jwt:PrivateKey`; compose env `JWT_SECRET_KEY` → `JWT_PRIVATE_KEY`.

- [ ] **Step 1: Create the key holder**

`src/PriceNegotiationApp.Modules.Identity/Features/Auth/EcSigningKey.cs`:

```csharp
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace PriceNegotiationApp.Modules.Identity.Features.Auth;

/// <summary>
/// Holds the ES256 private key PEM and derives the public JWK once.
/// Only PublicJwk ever leaves this class: bearer validation and the JWKS
/// endpoint can verify tokens without holding signing material.
/// </summary>
internal sealed class EcSigningKey
{
    public const string Algorithm = SecurityAlgorithms.EcdSa256;

    private const string Usage =
        "Jwt:PrivateKey must be an EC P-256 private key in PKCS#8 PEM "
        + "(generate: openssl ecparam -name prime256v1 -genkey -noout).";

    private readonly string _privateKeyPem;

    internal JsonWebKey PublicJwk { get; }

    internal string Kid { get; }

    public EcSigningKey(IOptions<JwtOptions> options)
    {
        _privateKeyPem = Normalize(options.Value.PrivateKey);
        using var ecdsa = Import(_privateKeyPem);
        var jwk = JsonWebKeyConverter.ConvertFromECDsaPublicKey(ecdsa);
        Kid = jwk.ComputeJwkThumbprint();
        jwk.Kid = Kid;
        PublicJwk = jwk;
    }

    internal ECDsa CreatePrivateEcdsa() => Import(_privateKeyPem);

    private static ECDsa Import(string pem)
    {
        var ecdsa = ECDsa.Create();
        try
        {
            ecdsa.ImportFromPem(pem);
        }
        catch (CryptographicException ex)
        {
            ecdsa.Dispose();
            throw new InvalidOperationException(Usage, ex);
        }

        if (ecdsa.KeySize != 256)
        {
            ecdsa.Dispose();
            throw new InvalidOperationException(Usage);
        }

        return ecdsa;
    }

    private static string Normalize(string raw) => raw.Replace("\\n", "\n").Trim();
}
```

(A fresh `ECDsa` per sign call keeps concurrent requests off one crypto instance; parsing cost is negligible.)

- [ ] **Step 2: Swap the options property and validator**

`JwtOptions.cs` — replace `SecretKey` with:

```csharp
    /// <summary>EC P-256 private key, PKCS#8 PEM; newlines may be literal or \n-escaped.</summary>
    public required string PrivateKey { get; init; }
```

`JwtOptionsValidator.cs` — replace the secret-length check with:

```csharp
        if (string.IsNullOrWhiteSpace(options.PrivateKey))
        {
            failures.Add("Jwt:PrivateKey is required (ES256 PKCS#8 PEM; malformed keys fail at startup with generation instructions).");
        }
```

(Deep PEM parsing stays in `EcSigningKey`'s constructor — single source of truth.)

- [ ] **Step 3: Sign with ES256**

`JwtManager.cs` — constructor takes `EcSigningKey signingKey`; the credentials block becomes:

```csharp
        using var ecdsa = signingKey.CreatePrivateEcdsa();
        var credentials = new SigningCredentials(
            new ECDsaSecurityKey(ecdsa) { KeyId = signingKey.Kid },
            EcSigningKey.Algorithm);
```

Everything else in `Generate` is unchanged (`using System.Text;` becomes unused — remove it).

`IdentityModule.cs` — register the singleton next to `JwtManager`:

```csharp
        services.AddSingleton<EcSigningKey>();
        services.AddSingleton<JwtManager>();
```

- [ ] **Step 4: Validate with the public key**

In `WebApplicationBuilderExtensions.cs`, the `Configure<IOptions<JwtOptions>>` lambda from Task 4 gains the dependency and algorithm pinning:

```csharp
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, EcSigningKey>((bearer, jwt, signingKey) =>
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Value.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Value.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey.PublicJwk,
                    ValidAlgorithms = [EcSigningKey.Algorithm],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                });
```

Add `using PriceNegotiationApp.Modules.Identity.Features.Auth;`; remove `System.Text` if unused.

- [ ] **Step 5: Publish JWKS**

In `PipelineExtensions.MapModules`, before the health mappings:

```csharp
        app.MapGet("/.well-known/jwks.json", (Features.Auth.EcSigningKey signingKey) => TypedResults.Json(
                new JwksResponse([new JwkKey(
                    signingKey.PublicJwk.Kty,
                    signingKey.PublicJwk.Crv,
                    signingKey.PublicJwk.X,
                    signingKey.PublicJwk.Y,
                    signingKey.Kid)])))
            .AllowAnonymous();
```

Add to `IsInfrastructurePath`: `path.StartsWithSegments("/.well-known", StringComparison.OrdinalIgnoreCase) ||`.

Append below the class:

```csharp
internal sealed record JwksResponse(IReadOnlyList<JwkKey> Keys);

// Deliberate DTO: serializes exactly the five public fields, so private material
// can never leak even if JsonWebKey grows properties later.
internal sealed record JwkKey(string Kty, string Crv, string X, string Y, string Kid);
```

- [ ] **Step 6: Config churn**

`appsettings.json` — inside `Jwt`, replace `"SecretKey": ""` with `"PrivateKey": ""`.

`docker-compose.yml:12` — replace the `Jwt__SecretKey` line:

```yaml
      Jwt__PrivateKey: ${JWT_PRIVATE_KEY:?set JWT_PRIVATE_KEY (ES256 PKCS#8 PEM, see README)}
```

`.env.example` — replace the `JWT_SECRET_KEY` line:

```
# ES256 signing key: PKCS#8 PEM generated per README; escape newlines as \n for one line
JWT_PRIVATE_KEY=replace-with-key-generated-per-README
```

`README.md` — in Quickstart, replace the `Jwt:SecretKey` user-secret line with a generate-then-load pair, and add the PowerShell-native generator right after:

```bash
openssl ecparam -name prime256v1 -genkey -noout -out jwt-es256.pem
dotnet user-secrets set "Jwt:PrivateKey" "(Get-Content -Raw jwt-es256.pem)" --project src/PriceNegotiationApp.Api
```

```powershell
$ec = [System.Security.Cryptography.ECDsa]::Create([System.Security.Cryptography.ECCurve]::NamedCurves.nistP256)
[IO.File]::WriteAllText("$PWD/jwt-es256.pem", $ec.ExportPkcs8PrivateKeyPem())
```

For docker-compose, document escaping newlines for a single-line `.env` value:

```powershell
$env:JWT_PRIVATE_KEY = ((Get-Content -Raw jwt-es256.pem) -replace "`r?`n", "\n")
```

Update the Configuration table row: `Jwt:Issuer` / `Jwt:Audience` / `Jwt:PrivateKey` (ES256 PKCS#8 PEM, ≥ P-256) / `Jwt:ExpiryMinutes`. Also refresh the Stack table's Identity row wording to "ASP.NET Core Identity + JWT Bearer (ES256, strict issuer/audience/lifetime validation)".

- [ ] **Step 7: Point the integration fixture at an ephemeral key**

`IntegrationTestFactory.cs` — replace the `Jwt:SecretKey` setting:

```csharp
    private static readonly string SigningPem = CreateSigningPem();

    private static string CreateSigningPem()
    {
        using var ecdsa = System.Security.Cryptography.ECDsa.Create(
            System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        return ecdsa.ExportPkcs8PrivateKeyPem();
    }
```

and inside `ConfigureWebHost`:

```csharp
        builder.UseSetting("Jwt:PrivateKey", SigningPem);
```

Search for stragglers: `rg "SecretKey"` — remaining hits must only be inside this plan/spec docs, or in `JwtManagerShould` being rewritten next.

- [ ] **Step 8: Rewrite the unit tests**

Full content of `tests/PriceNegotiationApp.Modules.Identity.Tests/JwtManagerShould.cs`:

```csharp
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PriceNegotiationApp.Modules.Identity.Features.Auth;
using PriceNegotiationApp.TestKit;
using Shouldly;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace PriceNegotiationApp.Modules.Identity.Tests;

public class JwtManagerShould
{
    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }

    private static (JwtManager Manager, EcSigningKey Key) BuildSut()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var options = Options.Create(new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            PrivateKey = ecdsa.ExportPkcs8PrivateKeyPem(),
            ExpiryMinutes = 30,
        });
        var key = new EcSigningKey(options);
        return (new JwtManager(key, new FixedTimeProvider()), key);
    }

    private static TokenValidationParameters Parameters(EcSigningKey key) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = "test-issuer",
        ValidateAudience = true,
        ValidAudience = "test-audience",
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = key.PublicJwk,
        ValidAlgorithms = [EcSigningKey.Algorithm],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
    };

    [Fact]
    public void Generate_es256_token_with_kid_email_role_and_expiry()
    {
        var email = Fuzz.Email();
        var (sut, _) = BuildSut();

        var (token, expiresAtUtc) = sut.Generate(Guid.NewGuid(), email, ["Customer"]);

        var parts = token.Split('.');
        parts.Length.ShouldBe(3);
        var header = DecodeJson(parts[0]);
        header.GetProperty("alg").GetString().ShouldBe("ES256");
        header.GetProperty("kid").GetString().ShouldNotBeNullOrEmpty();
        DecodeJson(parts[1]).ShouldContain(email);
        var expected = new FixedTimeProvider().GetUtcNow().AddMinutes(30);
        (expiresAtUtc - expected).Duration().ShouldBeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Token_validates_against_the_published_public_key()
    {
        var userId = Guid.NewGuid();
        var (sut, key) = BuildSut();
        var (token, _) = sut.Generate(userId, Fuzz.Email(), ["Staff"]);

        var principal = new JwtSecurityTokenHandler().ValidateToken(token, Parameters(key), out _);

        principal!.FindFirst(ClaimTypes.NameIdentifier)!.Value.ShouldBe(userId.ToString());
        principal.FindFirst(ClaimTypes.Role)!.Value.ShouldBe("Staff");
    }

    [Fact]
    public void Token_signed_by_a_different_key_is_rejected()
    {
        var (sut, _) = BuildSut();
        var (_, stranger) = BuildSut();
        var (token, _) = sut.Generate(Guid.NewGuid(), Fuzz.Email(), []);

        Should.Throw<SecurityTokenException>(
            () => new JwtSecurityTokenHandler().ValidateToken(token, Parameters(stranger), out _));
    }

    private static JsonElement DecodeJson(string base64Url)
    {
        var padded = base64Url.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return JsonSerializer.Deserialize<JsonElement>(Convert.FromBase64String(padded));
    }
}
```

New `tests/PriceNegotiationApp.Modules.Identity.Tests/JwtOptionsValidatorShould.cs`:

```csharp
using Microsoft.Extensions.Options;
using PriceNegotiationApp.Modules.Identity.Features.Auth;
using Shouldly;
using Xunit;

namespace PriceNegotiationApp.Modules.Identity.Tests;

public class JwtOptionsValidatorShould
{
    private readonly JwtOptionsValidator _sut = new();

    [Fact]
    public void Accept_a_complete_configuration()
    {
        var result = _sut.Validate(null, new JwtOptions
        {
            Issuer = Fuzz.NewFaker().Internet.DomainName(),
            Audience = "price-negotiation-api",
            PrivateKey = "not-parsed-here",
            ExpiryMinutes = 30,
        });

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Reject_blank_private_key()
    {
        var result = _sut.Validate(null, new JwtOptions
        {
            Issuer = "i",
            Audience = "a",
            PrivateKey = "   ",
            ExpiryMinutes = 30,
        });

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(f => f.Contains("PrivateKey"));
    }

    [Fact]
    public void Reject_non_positive_expiry()
    {
        var result = _sut.Validate(null, new JwtOptions
        {
            Issuer = "i",
            Audience = "a",
            PrivateKey = "pem",
            ExpiryMinutes = 0,
        });

        result.Failed.ShouldBeTrue();
    }
}
```

New `tests/PriceNegotiationApp.IntegrationTests/JwksShould.cs`:

```csharp
using System.Net;
using System.Text.Json;
using PriceNegotiationApp.IntegrationTests.Support;
using Shouldly;
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
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return JsonSerializer.Deserialize<JsonElement>(Convert.FromBase64String(padded));
    }
}
```

- [ ] **Step 9: Run validation**

```bash
dotnet test --project tests/PriceNegotiationApp.Modules.Identity.Tests
dotnet test --project tests/PriceNegotiationApp.IntegrationTests
```

If `ComputeJwkThumbprint` or `ExportPkcs8PrivateKeyPem` fail to compile, confirm the transitive `Microsoft.IdentityModel.*` 8.x packages resolve; do not add new package references unless compilation proves one missing.

- [ ] **Step 10: Commit**

```bash
git add -A src tests docker-compose.yml .env.example README.md
git commit -m "feat(auth): sign tokens with ES256 and publish the public key via JWKS"
```

---

### Task 6: Supply-chain scan, findings report, deliberate trade-offs (spec B4)

**Files:**
- Modify: `docs/superpowers/specs/2026-08-26-security-review-design.md` (append executed findings table + finalized trade-offs)
- Possibly modify: `README.md` security notes (rate-limit posture sentence)

**Interfaces:**
- Consumes: everything implemented in Tasks 1–5.
- Produces: the review artifact — findings table with severities/evidence/status, trade-off ledger.

- [ ] **Step 1: Run the supply-chain scan**

```bash
dotnet list PriceNegotiationApp.slnx package --vulnerable --include-transitive
```

Record the outcome. Any hits: triage here (pin/bump in `Directory.Packages.props` within this task) or record as accepted risk with rationale in the spec appendix. Also skim `.github/dependabot.yml` covers both nuget and actions ecosystems (fix the config if it does not).

- [ ] **Step 2: Document rate-limit posture**

Append two sentences to the README Configuration section noting: auth endpoints are fixed-window limited per IP (default 30/min); the app expects direct exposure (compose) — put forwarded-header handling in front if deployed behind a reverse proxy.

- [ ] **Step 3: Append the findings report to the spec**

Append to `docs/superpowers/specs/2026-08-26-security-review-design.md`:

```markdown
## Executed findings (2026-08-26)

| ID | Severity | Finding | Evidence | Resolution |
|---|---|---|---|---|
| F1 | Medium | Anonymous `/health/ready` leaked unhealthy-check exception text | `ReadyHealthReport.WriteAsync` | Fixed: detail logged server-side only; body always `{status,durationMs}` |
| F2 | Low | Login distinguished locked accounts (`account_locked`) — enumeration oracle | `LoginUserHandler` | Fixed: every auth failure returns `invalid_credentials` |
| F3 | Medium | Seed credentials accepted 8-char passwords; examples shipped `Admin123!` | `SeedingOptionsValidator`, `.env.example` | Fixed: ≥12 chars mixed classes; placeholders fail fast if deployed |
| F4 | Info | Per-IP fixed-window limit assumes direct exposure (no forwarded headers) | `AddRateLimiter` | Accepted: documented proxy posture in README |
| F5 | Low | Duplicate JWT config contract skipped expiry validation | `Api/Extensions/JwtSettings.cs` | Fixed: deleted; bearer binds module-validated `JwtOptions` |
| F6 | Info | No limiter on authenticated writes | endpoint map | Accepted: authenticated abuse is rate-limited upstream in real deployments; revisit with real traffic profile |
| F7 | High | Shared HMAC secret made every replica a token minter; blocked issuance/validation split | `JwtManager`, bearer setup | Fixed: ES256 key pair, `kid`, JWKS publication |

## Deliberate trade-offs

- Short-lived access tokens only; no refresh/revocation machinery until multi-device sessions exist.
- Every replica holds the signing key because login runs everywhere; JWKS is the extraction path when issuance centralizes.
- Manual key rotation supported (`kid` in JWKS, validators trust the published set); no automated rotation.
- Registration conflict responses still confirm existing emails (standard UX trade-off); the login path itself is uniform. Timing side-channel between unknown-email and wrong-password paths remains (one PBKDF2 evaluation) — acceptable at portfolio threat level.
- Readiness failure detail is available in server logs, not the anonymous HTTP body.
```

Fill severity/status cells from actual execution observations; add rows for anything discovered mid-implementation.

- [ ] **Step 4: Full-suite gate**

```bash
dotnet format --verify-no-changes
dotnet build -c Release
dotnet test --solution PriceNegotiationApp.slnx -c Release
```

All three must pass clean (CI parity).

- [ ] **Step 5: Commit**

```bash
git add docs README.md Directory.Packages.props
git commit -m "docs(security): record review findings, supply-chain scan, trade-offs"
```
