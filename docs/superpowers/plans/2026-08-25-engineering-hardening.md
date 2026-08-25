# Engineering Hardening Implementation Plan (E-01/03/04/10/12/13/18 + E-11)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Land the eight engineering-hardening items from `docs/superpowers/specs/2026-08-25-engineering-hardening-design.md`: local telemetry dashboard, meaningful request logs, readiness detail JSON, SDK pin, centralized test conventions, CI-only deterministic build support, fail-fast startup validation, and an uncommitted local `nuget.config`.

**Architecture:** Build-system changes come first (targets/pins/global package reference), then four independent Api-side code items (logging, health writer, OTLP gating + compose overlay, options validation), then the deliberately-uncommitted `nuget.config`. No business behavior changes anywhere.

**Tech Stack:** .NET 10 SDK / MSBuild (`Directory.Build.props|targets`), Serilog request logging, ASP.NET Core health checks, OpenTelemetry `.UseOtlpExporter()`, Aspire Dashboard container, Microsoft.Testing.Platform.

## Global Constraints

- Source spec: `docs/superpowers/specs/2026-08-25-engineering-hardening-design.md`.
- Local SDK is `10.0.303`; pin uses `"version": "10.0.303"`, `"rollForward": "latestFeature"`.
- Base `docker-compose.yml` must remain byte-for-byte untouched; observability goes in a new override file.
- `/health/live` output and semantics unchanged; only `/health/ready` gains a JSON body writer.
- `Deterministic=true` etc. are already SDK defaults — do NOT re-add them (spec §6).
- `CatalogSeedingOptions` gets **no** validator (single bool — spec §7); instead add a one-line comment noting the deliberate omission.
- Validators replicate the existing JWT pattern: `AddOptions<T>().Bind(...)` (+`.ValidateOnStart()` where wired) plus `AddSingleton<IValidateOptions<T>, TV>()`.
- Identity internals are visible to its own test project; **Api internals are visible to nobody** — the two new Api validator/guard classes must be `public`.
- Every task ends with `dotnet build` zero warnings and touched test projects green.
- All commands run from repo root in pwsh.
- Integration tests need Docker (Testcontainers postgres:17-alpine); if unavailable, say so plainly instead of skipping silently.
- **E-11 (`nuget.config`) is created in Task 8, LAST, and must never be `git add`ed or committed.**

---

### Task 1: Centralize test-project conventions

**Files:**
- Create: `Directory.Build.targets`
- Modify: `tests/PriceNegotiationApp.Modules.Negotiations.Tests/PriceNegotiationApp.Modules.Negotiations.Tests.csproj`
- Modify: `tests/PriceNegotiationApp.Modules.Identity.Tests/PriceNegotiationApp.Modules.Identity.Tests.csproj`
- Modify: `tests/PriceNegotiationApp.Modules.Catalog.Tests/PriceNegotiationApp.Modules.Catalog.Tests.csproj`
- Modify: `tests/PriceNegotiationApp.IntegrationTests/PriceNegotiationApp.IntegrationTests.csproj`
- Modify: `tests/PriceNegotiationApp.ArchitectureTests/PriceNegotiationApp.ArchitectureTests.csproj`

**Interfaces:**
- Consumes: nothing.
- Produces: implicit build conventions for any project whose name contains `.Tests` — `OutputType=Exe`, `IsPackable=false`, `NoWarn += CA1707;S1118`. Later tasks rely on csprojs NOT redeclaring these.

- [ ] **Step 1: Create the targets file**

`Directory.Build.targets` (repo root):

```xml
<Project>
  <!-- Conventions for every test project; keeps individual test csprojs reference-only. -->
  <PropertyGroup Condition="$(MSBuildProjectName.Contains('.Tests'))">
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
    <NoWarn>$(NoWarn);CA1707;S1118</NoWarn>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Slim the five test csprojs**

In each of the five files listed above, delete the entire `<PropertyGroup>` block containing `<OutputType>Exe</OutputType>` and `<NoWarn>$(NoWarn);CA1707;S1118</NoWarn>` (IntegrationTests/Negotiations/Identity/Catalog have exactly that two-property block; ArchitectureTests has the same). Keep every other line — project references, package references — untouched. Example result for Negotiations.Tests:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\..\src\PriceNegotiationApp.Modules.Negotiations\PriceNegotiationApp.Modules.Negotiations.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Bogus" />
    <PackageReference Include="Microsoft.Testing.Extensions.CodeCoverage" />
    <PackageReference Include="Shouldly" />
    <PackageReference Include="xunit.v3" />
  </ItemGroup>
</Project>
```

Apply the same deletion to the other four; their reference items stay as-is.

- [ ] **Step 3: Validate**

```bash
dotnet build && dotnet test tests/PriceNegotiationApp.Modules.Negotiations.Tests --no-build
```

Build succeeds with zero warnings; all unit tests pass. If MSBuild complains about the condition syntax, the correct form is `Condition="'$(MSBuildProjectName.Contains('.Tests'))'"`.

- [ ] **Step 4: Commit**

```bash
git add Directory.Build.targets tests/
git commit -m "build: centralize test project conventions in Directory.Build.targets"
```

---

### Task 2: Pin the SDK

**Files:**
- Modify: `global.json`

**Interfaces:**
- Consumes: nothing.
- Produces: deterministic SDK resolution (10.0.3xx feature band) for all later tasks and CI.

- [ ] **Step 1: Add the sdk section**

Full file content:

```json
{
  "sdk": {
    "version": "10.0.303",
    "rollForward": "latestFeature"
  },
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

- [ ] **Step 2: Validate**

```bash
dotnet --version
dotnet build --no-restore
```

`dotnet --version` prints a `10.0.3xx` value (locally expected exactly `10.0.303`); build still succeeds. If it prints a lower band or errors about a missing SDK, run `dotnet sdk check` and install the 10.0.3xx band before proceeding.

- [ ] **Step 3: Commit**

```bash
git add global.json
git commit -m "build: pin dotnet sdk to the 10.0.3xx feature band"
```

---

### Task 3: CI-only deterministic compilation + source link

**Files:**
- Modify: `Directory.Build.props`

**Interfaces:**
- Consumes: GitHub Actions' automatic `CI=true` environment variable.
- Produces: reproducible PDBs linked to the GitHub commit when built in CI; no local behavior change.

- [ ] **Step 1: Extend Directory.Build.props**

Append inside the existing top-level `<Project>` element, after the current `<PropertyGroup>`:

```xml
  <PropertyGroup>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
  </PropertyGroup>
  <!-- GitHub Actions exports CI=true; keep local builds byte-identical to today. -->
  <PropertyGroup Condition="'$(CI)' == 'true'">
    <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
  </PropertyGroup>
  <ItemGroup>
    <GlobalPackageReference Include="Microsoft.SourceLink.GitHub" Version="8.0.0" PrivateAssets="all" />
  </ItemGroup>
```

(`Deterministic` stays unset on purpose — the SDK defaults it to true; see spec §6.)

- [ ] **Step 2: Validate both modes**

```bash
dotnet build --no-restore
$env:CI='true'; dotnet build --no-restore; Remove-Item Env:CI
```

Both succeed with zero warnings. The second simulates the CI property path.

- [ ] **Step 3: Commit**

```bash
git add Directory.Build.props
git commit -m "build: reproducible ci compilation with source link"
```

---

### Task 4: Enriched request logs with noise suppression

**Files:**
- Modify: `src/PriceNegotiationApp.Api/Extensions/PipelineExtensions.cs`

**Interfaces:**
- Consumes: existing Serilog pipeline (`app.UseSerilogRequestLogging()` currently parameterless at PipelineExtensions.cs:14).
- Produces: diagnostic context keys `UserId`, `Roles`, `Endpoint`, `RemoteIp` on every non-suppressed request record.

- [ ] **Step 1: Replace the request-logging call**

In `PipelineExtensions.cs`, replace line `app.UseSerilogRequestLogging();` with:

```csharp
        app.UseSerilogRequestLogging(options =>
        {
            // Enrichment runs at response completion, so HttpContext.User is populated.
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("UserId",
                    httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier));
                diagnosticContext.Set("Roles", string.Join(',',
                    httpContext.User.FindAll(ClaimTypes.Role).Select(claim => claim.Value)));
                diagnosticContext.Set("Endpoint", httpContext.GetEndpoint()?.DisplayName);
                diagnosticContext.Set("RemoteIp",
                    httpContext.Connection.RemoteIpAddress?.ToString());
            };
            options.GetLevel = (httpContext, elapsed, ex) => ex is not null
                ? LogEventLevel.Error
                : IsInfrastructurePath(httpContext.Request.Path)
                    ? LogEventLevel.Verbose
                    : elapsed > 500 ? LogEventLevel.Warning : LogEventLevel.Information;
        });
```

Add the helper at the bottom of the class:

```csharp
    private static bool IsInfrastructurePath(PathString path) =>
        path.StartsWithSegments("/health") ||
        path.StartsWithSegments("/scalar") ||
        path.StartsWithSegments("/openapi") ||
        path.StartsWithSegments("/favicon");
```

Extend the file's usings with:

```csharp
using System.Security.Claims;
using Serilog.Events;
```

- [ ] **Step 2: Validate**

```bash
dotnet build
```

Zero warnings. (Behavioral smoke-check happens naturally in Task 6's dashboard run, where suppressed paths stop appearing.)

- [ ] **Step 3: Commit**

```bash
git add src/PriceNegotiationApp.Api/Extensions/PipelineExtensions.cs
git commit -m "feat(api): enrich request logs and suppress infrastructure noise"
```

---

### Task 5: Readiness endpoint reports per-dependency detail

**Files:**
- Create: `src/PriceNegotiationApp.Api/ReadyHealthReport.cs`
- Modify: `src/PriceNegotiationApp.Api/Extensions/PipelineExtensions.cs` (ready `MapHealthChecks` block, ~line 40)
- Test: `tests/PriceNegotiationApp.IntegrationTests/ReadyHealthShould.cs`

**Interfaces:**
- Consumes: existing health-check registrations (`database-identity`, `database-catalog`, `database-negotiations`, all tagged `ready`; note the `self` check is tagged `live` only and therefore absent from readiness output).
- Produces: `static Task ReadyHealthReport.WriteAsync(HttpContext, HealthReport)` — JSON body `{ status, totalDurationMs, entries: { <name>: { status, durationMs, description? } } }`; `description` present only when a check is not Healthy.

- [ ] **Step 1: Create the writer**

`src/PriceNegotiationApp.Api/ReadyHealthReport.cs`:

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PriceNegotiationApp.Api;

/// <summary>JSON body for /health/ready naming every dependency and its verdict.</summary>
public static class ReadyHealthReport
{
    public static async Task WriteAsync(HttpContext context, HealthReport report)
    {
        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            entries = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.Status == HealthStatus.Healthy
                    ? (object)new
                    {
                        status = entry.Value.Status.ToString(),
                        durationMs = entry.Value.Duration.TotalMilliseconds,
                    }
                    : new
                    {
                        status = entry.Value.Status.ToString(),
                        durationMs = entry.Value.Duration.TotalMilliseconds,
                        description = entry.Value.Description ?? entry.Value.Exception?.Message,
                    }),
        };

        await context.Response.WriteAsJsonAsync(payload);
    }
}
```

- [ ] **Step 2: Wire it into the ready probe**

In `PipelineExtensions.MapModules`, replace:

```csharp
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });
```

with:

```csharp
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("ready"),
            ResponseWriter = ReadyHealthReport.WriteAsync,
        });
```

The `/health/live` line above it stays untouched. Default `HealthCheckOptions.ResultStatusCodes` already maps Unhealthy → 503.

- [ ] **Step 3: Add the integration test**

Create `tests/PriceNegotiationApp.IntegrationTests/ReadyHealthShould.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PriceNegotiationApp.IntegrationTests.Support;
using Shouldly;
using Xunit;

namespace PriceNegotiationApp.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ReadyHealthShould(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task Ready_reports_json_status_per_dependency()
    {
        var response = await fixture.Anonymous.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            cancellationToken: TestContext.Current.CancellationToken);

        body.GetProperty("status").GetString().ShouldBe("Healthy");
        body.GetProperty("totalDurationMs").GetDouble().ShouldBeGreaterThanOrEqualTo(0);

        var entries = body.GetProperty("entries");
        foreach (var name in new[] { "database-identity", "database-catalog", "database-negotiations" })
        {
            entries.TryGetProperty(name, out _).ShouldBeTrue($"missing health entry '{name}'");
            entries.GetProperty(name).GetProperty("status").GetString().ShouldBe("Healthy");
            entries.GetProperty(name).TryGetProperty("description", out _)
                .ShouldBeFalse("healthy checks must not carry a description");
        }
    }
}
```

(The pre-existing `Ready_endpoint_reports_all_module_schemas` fact in `NegotiationsShould.cs` still passes — the new payload contains the substring `Healthy`.)

- [ ] **Step 4: Validate**

```bash
dotnet build && dotnet test tests/PriceNegotiationApp.IntegrationTests --no-build
```

Requires Docker. Both ready-related facts green.

- [ ] **Step 5: Commit**

```bash
git add src/PriceNegotiationApp.Api/ReadyHealthReport.cs src/PriceNegotiationApp.Api/Extensions/PipelineExtensions.cs tests/PriceNegotiationApp.IntegrationTests/ReadyHealthShould.cs
git commit -m "feat(api): readiness endpoint reports per-dependency detail"
```

---

### Task 6: Opt-in OTLP export + Aspire Dashboard compose overlay

**Files:**
- Modify: `src/PriceNegotiationApp.Api/Extensions/WebApplicationBuilderExtensions.cs` (OpenTelemetry block near the end of `AddApiServices`)
- Create: `compose.observability.yml`
- Modify: `README.md` (Health & telemetry section)

**Interfaces:**
- Consumes: existing `.UseOtlpExporter()` registration; standard `OTEL_EXPORTER_OTLP_ENDPOINT` environment variable.
- Produces: telemetry exported only when an endpoint is configured; `compose.observability.yml` runnable as `-f docker-compose.yml -f compose.observability.yml up`.

- [ ] **Step 1: Gate the exporter on endpoint presence**

In `WebApplicationBuilderExtensions.AddApiServices`, replace the entire `builder.Services.AddOpenTelemetry()...UseOtlpExporter();` block with:

```csharp
        // Telemetry ships only when a consumer is configured (Aspire dashboard overlay,
        // Grafana stack, or any OTLP endpoint). Prevents endless export retries against
        // localhost:4317 where nothing listens.
        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        if (!string.IsNullOrEmpty(otlpEndpoint))
        {
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService("PriceNegotiationApp.Api"))
                .WithTracing(tracing => tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation())
                .WithMetrics(metrics => metrics
                    .AddAspNetCoreInstrumentation()
                    .AddRuntimeInstrumentation())
                .UseOtlpExporter();
        }
```

- [ ] **Step 2: Create the overlay**

`compose.observability.yml` (repo root):

```yaml
services:
  api:
    environment:
      OTEL_EXPORTER_OTLP_ENDPOINT: http://aspire-dashboard:18889

  aspire-dashboard:
    image: mcr.microsoft.com/dotnet/aspire-dashboard:9.4
    environment:
      DASHBOARD__FRONTEND__AUTHMODE: Unsecured
    ports:
      - "127.0.0.1:18888:18888"
```

The OTLP receiver port (18889) stays internal to the compose network; only the UI is
published, loopback-only, unauthenticated — acceptable for a local demo surface.

- [ ] **Step 3: Document in README**

In `README.md`, directly under the `## Health & telemetry` bullet list, append:

````markdown
### Local telemetry dashboard

```bash
docker compose -f docker-compose.yml -f compose.observability.yml up --build
```

Aspire Dashboard UI: http://127.0.0.1:18888 — live traces, metrics and logs for every request.
For `dotnet run` development, start just the dashboard
(`docker compose -f docker-compose.yml -f compose.observability.yml up aspire-dashboard`)
and set the user secret `OTEL_EXPORTER_OTLP_ENDPOINT` to `http://localhost:18889`.
````

- [ ] **Step 4: Validate**

```bash
docker compose -f docker-compose.yml -f compose.observability.yml config --quiet
dotnet build && dotnet test tests/PriceNegotiationApp.IntegrationTests --no-build
```

The first command proves the merged compose model is valid; integration tests prove the
API boots identically without the endpoint set (exporter skipped, no retry noise).
Optional but recommended manual smoke: run the full overlay stack, hit
`POST /api/v1/auth/register`, confirm the trace appears at http://127.0.0.1:18888.

- [ ] **Step 5: Commit**

```bash
git add src/PriceNegotiationApp.Api/Extensions/WebApplicationBuilderExtensions.cs compose.observability.yml README.md
git commit -m "feat(ops): opt-in otlp export with aspire dashboard compose overlay"
```

---

### Task 7: Fail-fast startup configuration validation

**Files:**
- Create: `src/PriceNegotiationApp.Modules.Identity/Seeding/SeedingOptionsValidator.cs`
- Modify: `src/PriceNegotiationApp.Modules.Identity/IdentityModule.cs:43-44`
- Modify: `src/PriceNegotiationApp.Modules.Catalog/CatalogModule.cs:19-20`
- Create: `src/PriceNegotiationApp.Api/Extensions/RateLimitingOptionsValidator.cs`
- Create: `src/PriceNegotiationApp.Api/Extensions/CorsOriginsGuard.cs`
- Modify: `src/PriceNegotiationApp.Api/Extensions/WebApplicationBuilderExtensions.cs` (Cors + rate-limiter wiring)
- Test: `tests/PriceNegotiationApp.Modules.Identity.Tests/SeedingOptionsValidatorShould.cs`
- Test: `tests/PriceNegotiationApp.IntegrationTests/ConfigurationValidationShould.cs`

**Interfaces:**
- Consumes: the JWT precedent — `AddOptions<T>().Bind(section)` + `ValidateOnStart()` + `IValidateOptions<T>` singleton (IdentityModule.cs:36-39).
- Produces:
  - `SeedingOptionsValidator : IValidateOptions<SeedingOptions>` (internal, Identity module).
  - `RateLimitingOptionsValidator : IValidateOptions<RateLimitingOptions>` (**public** — Api internals are visible to no test project).
  - `static CorsOriginsGuard.EnsureValid(IEnumerable<string?>? origins)` (**public**) — throws `InvalidOperationException` naming the first bad origin.

- [ ] **Step 1: Identity seeding validator**

Create `src/PriceNegotiationApp.Modules.Identity/Seeding/SeedingOptionsValidator.cs`:

```csharp
using Microsoft.Extensions.Options;

namespace PriceNegotiationApp.Modules.Identity.Seeding;

internal sealed class SeedingOptionsValidator : IValidateOptions<SeedingOptions>
{
    public ValidateOptionsResult Validate(string? name, SeedingOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.AdminEmail) || !options.AdminEmail.Contains('@'))
        {
            failures.Add("Seeding:AdminEmail must be a non-empty email address.");
        }

        if (string.IsNullOrWhiteSpace(options.StaffEmail) || !options.StaffEmail.Contains('@'))
        {
            failures.Add("Seeding:StaffEmail must be a non-empty email address.");
        }

        if (string.IsNullOrWhiteSpace(options.AdminPassword) || options.AdminPassword.Length < 8)
        {
            failures.Add("Seeding:AdminPassword must be at least 8 characters.");
        }

        if (string.IsNullOrWhiteSpace(options.StaffPassword) || options.StaffPassword.Length < 8)
        {
            failures.Add("Seeding:StaffPassword must be at least 8 characters.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
```

Wire it in `IdentityModule.cs` — replace lines 43–44:

```csharp
        services.AddOptions<SeedingOptions>()
            .Bind(configuration.GetSection(SeedingOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<SeedingOptions>, SeedingOptionsValidator>();
```

(The following `services.AddHostedService<IdentitySeedingHostedService>();` line stays.)

- [ ] **Step 2: Catalog deliberate-omission comment**

In `CatalogModule.cs`, replace lines 19–20 with:

```csharp
        // Deliberately unvalidated: CatalogSeedingOptions is a single optional bool
        // with no meaningful validation surface (engineering-hardening spec §7).
        services.AddOptions<CatalogSeedingOptions>()
            .Bind(configuration.GetSection(CatalogSeedingOptions.SectionName));
```

- [ ] **Step 3: Api validators**

Create `src/PriceNegotiationApp.Api/Extensions/RateLimitingOptionsValidator.cs`:

```csharp
using Microsoft.Extensions.Options;

namespace PriceNegotiationApp.Api.Extensions;

public sealed class RateLimitingOptionsValidator : IValidateOptions<RateLimitingOptions>
{
    public ValidateOptionsResult Validate(string? name, RateLimitingOptions options) =>
        options.AuthPermitLimit >= 1
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"{RateLimitingOptions.SectionName}:AuthPermitLimit must be >= 1.");
}
```

Create `src/PriceNegotiationApp.Api/Extensions/CorsOriginsGuard.cs`:

```csharp
namespace PriceNegotiationApp.Api.Extensions;

public static class CorsOriginsGuard
{
    /// <summary>Throws at startup when a configured CORS origin is not an absolute http(s) URI.</summary>
    public static void EnsureValid(IEnumerable<string?>? origins)
    {
        foreach (var origin in origins ?? [])
        {
            var valid = Uri.TryCreate(origin, UriKind.Absolute, out var parsed)
                        && parsed.Scheme is "http" or "https";
            if (!valid)
            {
                throw new InvalidOperationException(
                    $"Cors:AllowedOrigins entry '{origin}' is not a valid absolute http(s) URI.");
            }
        }
    }
}
```

Both classes are `public` because Api internals are visible to no test assembly.

- [ ] **Step 4: Wire them into AddApiServices**

In `WebApplicationBuilderExtensions.cs`, replace the existing Cors block:

```csharp
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length > 0)
        {
            builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
                policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
        }
```

with:

```csharp
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        CorsOriginsGuard.EnsureValid(origins);
        if (origins.Length > 0)
        {
            builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
                policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
        }
```

Then, immediately after the existing `var rateLimits = ...` line, add the binder +
validator (the limiter itself keeps consuming the local `rateLimits` value — zero churn):

```csharp
        builder.Services.AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.SectionName))
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<RateLimitingOptions>,
            RateLimitingOptionsValidator>();
```

Add using `Microsoft.Extensions.Options;` to the file.

- [ ] **Step 5: Unit tests — identity validator**

Create `tests/PriceNegotiationApp.Modules.Identity.Tests/SeedingOptionsValidatorShould.cs`:

```csharp
using PriceNegotiationApp.Modules.Identity.Seeding;
using Shouldly;
using Xunit;

namespace PriceNegotiationApp.Modules.Identity.Tests;

public class SeedingOptionsValidatorShould
{
    private readonly SeedingOptionsValidator _sut = new();

    private static SeedingOptions Options(
        string adminEmail = "admin@app.com",
        string adminPassword = "Sup3rSecret!",
        string staffEmail = "staff@app.com",
        string staffPassword = "Sup3rSecret!") => new()
    {
        AdminEmail = adminEmail,
        AdminPassword = adminPassword,
        StaffEmail = staffEmail,
        StaffPassword = staffPassword,
    };

    [Fact]
    public void Accept_a_complete_configuration() =>
        _sut.Validate(null, Options()).Succeeded.ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Reject_invalid_admin_email(string? email) =>
        _sut.Validate(null, Options(adminEmail: email!)).Failed.ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    public void Reject_admin_password_shorter_than_identity_floor(string? password) =>
        _sut.Validate(null, Options(adminPassword: password!)).Failed.ShouldBeTrue();

    [Fact]
    public void Aggregate_every_violation_in_one_result()
    {
        var result = _sut.Validate(null, new SeedingOptions());

        result.Failed.ShouldBeTrue();
        result.Failures.Count.ShouldBe(4);
    }
}
```

(`SeedingOptions` has `init`-only properties and is internal — visible here via
InternalsVisibleTo. The `Options(...)` helper sidesteps the fact that plain classes
do not support `with` expressions.)

- [ ] **Step 6: Unit tests — Api validators**

Create `tests/PriceNegotiationApp.IntegrationTests/ConfigurationValidationShould.cs`
(plain facts; no Docker container needed for these two):

```csharp
using PriceNegotiationApp.Api.Extensions;
using Shouldly;
using Xunit;

namespace PriceNegotiationApp.IntegrationTests;

public class ConfigurationValidationShould
{
    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(int.MaxValue)]
    public void Accept_permit_limits_of_at_least_one(int limit) =>
        new RateLimitingOptionsValidator()
            .Validate(null, new RateLimitingOptions { AuthPermitLimit = limit })
            .Succeeded.ShouldBeTrue();

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Reject_non_positive_permit_limits(int limit) =>
        new RateLimitingOptionsValidator()
            .Validate(null, new RateLimitingOptions { AuthPermitLimit = limit })
            .Failed.ShouldBeTrue();

    [Fact]
    public void Accept_well_formed_cors_origins() =>
        Should.NotThrow(() => CorsOriginsGuard.EnsureValid(
            ["https://app.example.com", "http://localhost:3000"]));

    [Fact]
    public void Tolerate_null_or_empty_cors_lists() =>
        Should.NotThrow(() => CorsOriginsGuard.EnsureValid(null));

    [Theory]
    [InlineData("app.example.com")]
    [InlineData("ftp://app.example.com")]
    [InlineData("https://")]
    public void Reject_malformed_cors_origins(string origin) =>
        Should.Throw<InvalidOperationException>(
                () => CorsOriginsGuard.EnsureValid([origin]))
            .Message.ShouldContain(origin);
}
```

- [ ] **Step 7: Validate**

```bash
dotnet build && dotnet test tests/PriceNegotiationApp.Modules.Identity.Tests --no-build
dotnet test tests/PriceNegotiationApp.IntegrationTests --no-build --filter-class PriceNegotiationApp.IntegrationTests.ConfigurationValidationShould
```

All green; zero warnings. Startup-success path is additionally covered by every existing
integration test (they boot the real factory through the new validators).

- [ ] **Step 8: Commit**

```bash
git add src/PriceNegotiationApp.Modules.Identity src/PriceNegotiationApp.Modules.Catalog/CatalogModule.cs src/PriceNegotiationApp.Api/Extensions tests/PriceNegotiationApp.Modules.Identity.Tests/SeedingOptionsValidatorShould.cs tests/PriceNegotiationApp.IntegrationTests/ConfigurationValidationShould.cs
git commit -m "feat(config): fail-fast startup validation for seeding, rate limiting and cors"
```

---

### Task 8: Full validation + uncommitted nuget.config (E-11, LAST)

**Files:**
- Create (UNCOMMITTED): `nuget.config`

**Interfaces:**
- Consumes: everything from Tasks 1–7.
- Produces: a clean, fully validated tree plus one deliberately-untracked local hardening file.

- [ ] **Step 1: CI-parity validation of the whole tree**

```bash
dotnet format --verify-no-changes --no-restore
dotnet build -c Release --no-restore
dotnet test --solution PriceNegotiationApp.slnx -c Release --no-build --coverage --coverage-output-format cobertura
```

All five test projects green (format check runs first; fix with `dotnet format` and re-commit
if it flags anything). If Docker is unavailable, run unit + architecture projects only and
state the integration skip explicitly.

- [ ] **Step 2: Create nuget.config — DO NOT COMMIT**

Create `nuget.config` in the repo root:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <fallbackPackageFolders>
    <clear />
  </fallbackPackageFolders>
</configuration>
```

- [ ] **Step 3: Prove restore still works under the locked-down sources**

```bash
dotnet restore --force
dotnet build --no-restore -c Release
```

Both succeed purely from nuget.org. This is the acceptance test for E-11.

- [ ] **Step 4: Verify E-11 stays out of git**

```bash
git status --short
git log --oneline -8
```

`nuget.config` must appear as an untracked file (`??`) and appear in NO commit. Do not add
it to `.gitignore` either — it is intentionally present-but-local.

---

## Self-Review Record

- Spec coverage: §1 E-01 → Task 6; §2 E-03 → Task 4; §3 E-04 → Task 5; §4 E-10 → Task 2;
  §5 E-12 → Task 1; §6 E-13 → Task 3; §7 E-18 → Task 7 (Catalog omission comment included);
  §8 E-11 → Task 8 last/uncommitted; rollout order matches spec §9.
- Placeholder scan: none remaining — every code step carries complete file content;
  the Aspire image tag is pinned to `9.4`.
- Type consistency: `ReadyHealthReport.WriteAsync(HttpContext, HealthReport)` matches its
  wiring; validator class names (`SeedingOptionsValidator`, `RateLimitingOptionsValidator`,
  `CorsOriginsGuard.EnsureValid`) identical across creation/wiring/test steps; `IsInfrastructurePath`
  helper defined in Task 4 where used.

