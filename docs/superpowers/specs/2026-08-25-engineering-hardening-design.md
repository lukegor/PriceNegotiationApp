# Engineering Hardening Design — E-01, E-03, E-04, E-10, E-12, E-13, E-18, E-11

Date: 2026-08-25
Source backlog: `docs/engineering-backlog.md`
Scope: eight engineering-experience items. All are additive or config-level changes;
no business behavior changes. E-11 is implemented last and intentionally left
uncommitted.

---

## 1. E-01 — Local telemetry consumer (Aspire Dashboard)

### Decisions
- **Consumer:** standalone `mcr.microsoft.com/dotnet/aspire-dashboard` container
  (image pinned to the current `9.x` minor at implementation time). Rejected: full
  Grafana+Loki+Tempo provisioning (deferred as backlog E-02); Seq/custom collector.
- **Wiring:** a separate override file `compose.observability.yml` adds the dashboard
  service and injects `OTEL_EXPORTER_OTLP_ENDPOINT=http://aspire-dashboard:18889`
  into the api service. The **base `docker-compose.yml` remains untouched** — running
  without the override keeps production-shaped behavior identical to today.
- **Code change (required):** `AddApiServices` currently calls `.UseOtlpExporter()`
  unconditionally, which makes the exporter hammer `localhost:4317` inside containers
  where nothing listens. Gate registration on presence of the endpoint:

```csharp
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

- **Dashboard security:** `DASHBOARD__FRONTEND__AUTHMODE=Unsecured` acceptable because
  the UI port is published only on `127.0.0.1:18888`; OTLP receiver port `18889` stays
  internal to the compose network (no host publishing).
- **Local `dotnet run` flow:** start the dashboard via compose, then point the API at it
  with a user-secret/environment value `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:18889`.
  Documented in README (short subsection under Health & telemetry).

### Verification
`docker compose -f docker-compose.yml -f compose.observability.yml up --profile observability`
→ dashboard reachable at http://127.0.0.1:18888, traces/metrics/logs visible after hitting
any endpoint; plain `docker compose up` produces zero exporter error noise in api logs.

---

## 2. E-03 — Meaningful request logs

Single modification of the existing `UseSerilogRequestLogging()` call site:

```csharp
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("UserId", httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier));
        diagnosticContext.Set("Roles", string.Join(',', httpContext.User.FindAll(ClaimTypes.Role).Select(c => c.Value)));
        diagnosticContext.Set("Endpoint", httpContext.GetEndpoint()?.DisplayName);
        diagnosticContext.Set("RemoteIp", httpContext.Connection.RemoteIpAddress?.ToString());
    };
    options.GetLevel = (httpContext, elapsed, ex) => ex is not null
        ? LogEventLevel.Error
        : IsNoise(httpContext.Request.Path)
            ? LogEventLevel.Verbose
            : elapsed > 500 ? LogEventLevel.Warning : LogEventLevel.Information;
});
```

- `IsNoise` returns true for paths starting with `/health`, `/scalar`, `/openapi`,
  `/favicon` — those requests log at Verbose, invisible under the default Information
  sinks but available when debugging locally by flipping the minimum level.
- Slow-request threshold (500 ms) elevates otherwise-informational completions to Warning,
  making latency outliers grep-able.
- Enrichments read `HttpContext.User` inside `EnrichDiagnosticContext`, which executes at
  response completion — after authentication has populated the principal, despite the
  middleware sitting early in the pipeline.

Rejected: suppressing via a separate short-circuit middleware (loses even Verbose records),
and per-route logger scopes (redundant with Endpoint display name).

---

## 3. E-04 — Readiness detail surface

`/health/live` stays a bare liveness probe. `/health/ready` gains a custom writer:

```csharp
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready"),
    ResponseWriter = ReadyHealthReport.WriteAsync,
});
```

`ReadyHealthReport.WriteAsync` (new file in `Api`) serializes:

```json
{
  "status": "Unhealthy",
  "totalDurationMs": 42,
  "entries": {
    "database-identity": { "status": "Healthy", "durationMs": 5 },
    "database-catalog":  { "status": "Healthy", "durationMs": 4 },
    "database-negotiations": { "status": "Unhealthy", "durationMs": 30,
                               "description": "Connection refused (postgres:5432)" }
  }
}
```

Rules: `description` included **only** when `status != Healthy`; HTTP status code continues
to follow overall health (200/503) via the standard `HealthCheckOptions.ResultStatusCodes`
defaults. Anonymous access unchanged. System.Text.Json, camelCase, no source generation
needed for this fixed small shape.

---

## 4. E-10 — Pin the SDK

`global.json` becomes:

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

`latestFeature` accepts any newer patch within the 10.0.3xx feature band (local machine
runs 10.0.303 today; CI's `setup-dotnet@v4` with `10.0.x` resolves the newest 10.0.3xx).
Other bands and future majors are blocked until the pin is deliberately raised.

---

## 5. E-12 — Centralized test-project conventions

New `Directory.Build.targets` (auto-imported by every project):

```xml
<Project>
  <PropertyGroup Condition="$(MSBuildProjectName.Contains('.Tests'))">
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
    <NoWarn>$(NoWarn);CA1707;S1118</NoWarn>
  </PropertyGroup>
</Project>
```

All five test csprojs (`Modules.{Catalog,Identity,Negotiations}.Tests`,
`IntegrationTests`, `ArchitectureTests`) drop their duplicated `<PropertyGroup>` blocks;
they retain only project/package references. Naming condition (`.Tests` suffix) was chosen
over directory sniffing — deterministic and readable. Future test projects inherit the
conventions with zero ceremony.

---

## 6. E-13 — Deterministic/CI build support (non-default parts only)

`Deterministic` and `TreatWarningsAsErrors`-style flags are already SDK defaults — not
touched. Added to `Directory.Build.props`:

```xml
<PropertyGroup>
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
</PropertyGroup>
<PropertyGroup Condition="'$(CI)' == 'true'">
  <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
  <EmbedUntrackedSources>true</EmbedUntrackedSources>
</PropertyGroup>
<ItemGroup>
  <GlobalPackageReference Include="Microsoft.SourceLink.GitHub"
                          Version="8.0.0" PrivateAssets="all" />
</ItemGroup>
```

(GitHub Actions exports `CI=true`; the guard keeps local builds unaffected.) PDBs produced
in CI become reproducible and linked to the GitHub commit. No snupkg work — no packages
are packed today.

---

## 7. E-18 — Uniform startup configuration validation

Replicates the established JWT pattern (`AddOptions().Bind().ValidateOnStart()` +
`IValidateOptions<T>` singleton):

| Validator | Location | Rules |
|---|---|---|
| `SeedingOptionsValidator` | Identity module, `Seeding/` | `AdminEmail`/`StaffEmail` non-empty and contain `@`; `AdminPassword`/`StaffPassword` length ≥ 8 (ASP.NET Identity default floor) |
| `RateLimitingOptionsValidator` | Api, `Extensions/` | `AuthPermitLimit` ≥ 1 |
| Cors origins check | Api, `Extensions/` | Each entry in `Cors:AllowedOrigins` parses as absolute `http(s)` URI |

Registration mirrors `IdentityModule.cs:38-44` (`JwtOptions` precedent):
validators registered as `AddSingleton<IValidateOptions<T>, TV>();` with
`.ValidateOnStart()` on the corresponding `AddOptions<T>()` binder. Failures abort startup
with an `OptionsValidationException` aggregating every violated rule — identical semantics
to the existing JWT path.

**Explicitly skipped:** `CatalogSeedingOptions` — a single optional bool has no meaningful
validation surface (YAGNI). A comment in `CatalogModule` notes the deliberate omission.

### Testing
Unit tests per validator covering the happy path and each individual failure branch.
The two Api-owned validators are tested from the existing `PriceNegotiationApp.IntegrationTests`
assembly as plain xUnit facts (the project already references Api; these tests need no
Docker container). The Identity `SeedingOptionsValidator` tests live in
`PriceNegotiationApp.Modules.Identity.Tests` alongside the other module units.

---

## 8. E-11 — `nuget.config` supply-chain lockdown (LAST, NOT COMMITTED)

Implemented as the final step of the plan, deliberately excluded from any commit and left
as a local working-tree file:

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

Rationale for staying uncommitted: it is a personal-machine hardening preference; committing
would impose feed policy on every consumer of the portfolio repo. `packageSourceMapping`
is omitted — pointless with a single source.

---

## 9. Error handling & rollout notes

- All changes fail fast or degrade silently-by-design; none alter runtime business paths.
- Rollout order within implementation: E-12/E-10/E-13 (build files) → E-03/E-04/E-01+E-18
  (code) → E-11 last. Each item is independently revertible.
- Compatibility check after build-file changes: full clean restore + Release build +
  entire MTP test suite green before proceeding to code items.

## 10. Out of scope

Grafana/Loki/Tempo dashboards (backlog E-02), coverage reporting (E-05), image publishing
(E-08), OpenAPI XML docs (E-15) — later waves.
