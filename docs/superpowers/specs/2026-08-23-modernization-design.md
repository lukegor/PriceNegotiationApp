# PriceNegotiationApp — Full Modernization Design

Date: 2026-08-23
Status: Approved (design gate passed)
Mandate: Greenfield-in-place modernization of every layer — structure, API surface, domain, persistence, security, platform, tests, docs.

---

## 1. Goals & Non-Goals

### Goals
1. Fix all latent runtime bugs (broken `FindAsync` calls, one-negotiation-per-customer index, anonymous-user crash, broken test project reference).
2. Close all security gaps (committed secrets, disabled JWT validations, leaked exception details, unauthenticated response caching, no rate limiting).
3. Collapse 7 source projects to 4; delete all dead code, redundant DTO/mapper layers, and committed artifacts.
4. Replace the EF InMemory production database with PostgreSQL + checked-in migrations + Testcontainers-based integration tests.
5. Rebuild the API surface with minimal APIs, built-in .NET 10 validation, explicit filtering/paging, and a complete negotiation lifecycle (staff accept/decline is currently missing from the API despite the README advertising it).
6. Add missing platform pieces: Dockerfile, docker-compose, GitHub Actions CI, Dependabot, OpenTelemetry.
7. Rebuild the test suite around the negotiation lifecycle; remove FluentAssertions (v8 commercial-license risk).

### Non-Goals
- No frontend client.
- No message broker / event sourcing / CQRS framework (YAGNI at this scale).
- No multi-node concerns beyond stateless app design (JWT already stateless; DB is the only state).
- No asymmetric JWT signing (RS256) — noted as future work; HMAC-SHA256 stays for single-instance deployment.

---

## 2. Target Solution Structure

```
PriceNegotiationApp.slnx
Directory.Build.props          (unchanged: net10.0, nullable, warnings-as-errors)
Directory.Packages.props       (rewritten — see §10)
src/
  PriceNegotiationApp.Domain/            entities, value objects, business rules,
                                         policy, factories, domain exceptions
  PriceNegotiationApp.Application/       use-case services, ports, result types,
                                         request/response models per feature
  PriceNegotiationApp.Infrastructure/    EF Core 10 + Npgsql, Identity, JWT issuance,
                                         repositories, migrations, seeding
  PriceNegotiationApp.Api/               host: minimal-API endpoint groups, authz
                                         policies, ProblemDetails mapping, OpenAPI,
                                         health, Serilog+OTel, rate limiting, CORS
tests/
  PriceNegotiationApp.UnitTests/         domain + application logic (fast, no host)
  PriceNegotiationApp.IntegrationTests/  WebApplicationFactory + Testcontainers PG
```

**Deleted:** `Contracts`, `Presentation`, `SharedKernel`, empty root-level project folders, `logs/`, committed `PriceNegotiationApp.Api.json`, stale `.http` scratch entries.

### Dependency graph (strictly outward)
`Api → { Application, Infrastructure } → Domain`
- Api also references Infrastructure only for composition-root registration.
- Domain references **no packages except Vogen** (source-generator/attributes only).
- Application references **no ASP.NET packages** (no FrameworkReference, no Identity types, no OData). It defines ports; Infrastructure implements them.
- SharedKernel dissolves: ID value objects (`ProductId`, `NegotiationId`, `CustomerId`) move to `Domain/ValueObjects/Ids` under correct namespaces; EF converters are configured in Infrastructure using Vogen-generated converter classes.

### Program.cs split
Thin `Program.cs` (~30 lines: builder → module registration → pipeline → run) plus:
- `Api/Modules/ProductsModule.cs`, `NegotiationsModule.cs`, `AuthModule.cs` (`MapXxx(this IEndpointRouteBuilder)` endpoint groups)
- `Api/Extensions/{ServiceCollectionExtensions, PipelineExtensions}.cs`

---

## 3. Domain Model

### Value objects
- **IDs** (Vogen): `ProductId`, `NegotiationId`, `CustomerId` — `[ValueObject<Guid>(conversions: EfCore)]`.
- **Price**: single value object for base prices and proposals. Invariants: `Value > 0`, precision `decimal(18,2)`. Replaces hand-written `ProductPrice` and `ProposedPrice`.

### Enums
```csharp
enum NegotiationStatus { Open = 1, Accepted = 2, Declined = 3 }
enum NegotiationOutcome { CounterProposed = 1, AutoRejected = 2, NoProposalsRemaining = 3 }
```
The string-record status class, unused `Archived` status, and duplicated `NegotiationOutcome`-vs-status modeling are deleted.

### Policy (single source of truth)
```csharp
interface INegotiationPolicy
{
    int MaxProposalsPerNegotiation { get; }      // 3 — total proposals including initial
    decimal ProposalMultiplierLimit { get; }     // 2.0 — auto-reject above base × limit
}
```
Implementation in Domain (`DefaultNegotiationPolicy`), injected into factories/services. All `[Range]` annotations, magic numbers, and the Application-layer duplicate policy are deleted.

### Negotiation semantics (made explicit)
- `Negotiation.Start(product, customer, proposedPrice, time, policy)`:
  - Validates proposal ≤ `base × multiplier` else throws `ProposalExceedsLimitDomainException` (creation-time violation is caller error → 400).
  - Sets `BasePrice` snapshot (product price at creation), `CurrentOffer = proposedPrice`, `Status = Open`, `ProposalsUsed = 1`. Snapshot protects ongoing negotiations from product price changes.
- `CounterPropose(price, time)` returns `NegotiationOutcome` (normal flow, not an exception):
  - Requires `Status == Open` (else `NegotiationClosedDomainException` → mapped to 409).
  - Budget guard first: if `ProposalsUsed >= Max`, return `NoProposalsRemaining` (status unchanged; service maps to 409 with remaining=0).
  - If `price > BasePrice × multiplier`: `Status = Declined`, return `AutoRejected`.
  - Otherwise store: `CurrentOffer = price`, `ProposalsUsed += 1`, `LastProposalAtUtc = time`, return `CounterProposed`.
- `Accept(time)` / `Decline(time)` (staff decisions): require `Status == Open`; set terminal status + `DecidedAtUtc`. `Decline` on a negotiation with `ProposalsUsed >= Max` also yields `Declined` (terminal either way).
- `Withdraw` is not a status: customers delete their own Open negotiations (hard delete, history gone by design; Admin delete likewise).
- Removed entirely: `Archive()`, `ResetRetries()` (+ its endpoint), `[Obsolete] UpdateNegotiationAsync`, `[NonAction]` PUT endpoint.

### Product & Customer
- `Product`: `Id`, `Name` (≤200 chars), `Price` (Price VO), `Update(name, price)` keeps no-op guard rule. Rules stay as IBusinessRule implementations.
- `Customer`: retained as identity-linked profile row (`Id`, `IdentityUserId` unique); created lazily on first negotiation.
- `DateTimeOffset.UtcNow` never called inside entities — time always injected via parameter or `TimeProvider`.

### Concurrency
`uint Version` mapped to PostgreSQL `xmin` system column on `Product` and `Negotiation` → optimistic concurrency; DB exception maps to 409 ProblemDetails.

---

## 4. Persistence (Infrastructure)

- Provider: **Npgsql EF Core 10**, snake_case naming via `EFCore.NamingConventions`.
- `AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>` — internal to Infrastructure; **no `IAppDbContext` interface** (deleted).
- Ports defined in Application, implemented here:
  - `IProductRepository`: `GetByIdAsync`, `Query()` (IQueryable for composition in read paths), `AddAsync`, `Remove`, plus `IUnitOfWork.SaveChangesAsync(ct)` shared interface.
  - `INegotiationRepository`: same shape scoped to aggregate root.
  - `IUserAccountStore`: register / find-by-email / check-password / roles (implemented over UserManager/SignInManager — ASP.NET Identity stays, it correctly handles hashing/lockout/validation).
- Configurations (explicit `IEntityTypeConfiguration<T>`):
  - Product: OwnsOne `Price`; Name maxlen 200.
  - Negotiation: FKs Product/Customer; **partial unique index `(product_id, customer_id) WHERE status = 'Open'`** (one open negotiation per customer per product; closed history preserved — fixes the old whole-table `UserId` unique index bug); status stored as smallint; xmin version.
  - Customer: unique index `identity_user_id`.
  - ApplicationUser: drop custom `Role` string property (duplicate role claims fix) — roles live only in Identity role store.
- Migrations checked in under `Infrastructure/Data/Migrations`; startup hosted service runs `Database.MigrateAsync()` then idempotent seeding.
- Seeding (config-driven, never hardcoded secrets): roles Admin/Staff/Customer; admin + staff accounts from configuration section (compose supplies env vars; dev uses user-secrets); sample products seeded only in Development.

## 5. Authentication & Authorization

### JWT
- Validation hardened: `ValidateIssuer = true`, `ValidateAudience = true`, `ValidateLifetime = true` (all against configured values), valid issuer/audience bound from options; clock skew default.
- Secret: ≥32 chars, enforced by real options validator (`ValidateOnStart`) replacing the TODO stub; supplied via user-secrets (dev) / environment (compose, prod).
- Token contents: `sub`, `email`, `jti`, `iat`, role claims from Identity role store (single source; no custom claim duplication).
- Login lockout enabled: 5 failed attempts → 15 min lockout.

### Roles & policies
- Role constants in Domain-adjacent shared location (`UserRoles` static class in Application): `Admin`, `Staff`, `Customer`.
- Registration is public but always assigns `Customer` role. Staff/Admin exist only via seeding.
- Endpoint gating: role constants in policy strings (e.g., `RequireRoles(UserRoles.Admin, UserRoles.Staff)` helper extension replacing magic strings).
- Ownership checks become plain data comparisons inside application services: services receive a `CallerContext { UserId, IsInRole(...) }` record resolved in Api from `ClaimsPrincipal`. The resource-based `AuthorizationHandler`/`Operations` classes are deleted (they existed to work around services calling `IAuthorizationService`).

### Error contract
`ProblemDetails` everywhere with stable machine-readable extension property `code`:
| Situation | HTTP | code |
|---|---|---|
| Validation failure | 400 | `validation_failed` |
| Entity not found | 404 | `{entity}_not_found` |
| Negotiation closed/terminal | 409 | `negotiation_closed` |
| No proposals remaining | 409 | `no_proposals_remaining` |
| Proposal exceeds limit | 400 | `proposal_exceeds_limit` |
| Optimistic concurrency | 409 | `conflict` |
| Auth required / bad credentials | 401 | `unauthorized` |
| Forbidden | 403 | `forbidden` |
Production responses contain no internal exception text (`ExceptionDetail` shown only in Development). The 499-mapped `OperationCanceledException` handler is kept (client-abort is legitimate telemetry).

---

## 6. API Surface (minimal APIs)

Version prefix `/api/v1`. All endpoints accept `CancellationToken`. Success responses use `TypedResults` (typed `Ok<>`, `Created<>`, `NoContent`). Request records are plain DTOs with no validation attributes; all input invariants are enforced by the domain (entity rules, value objects, Identity policy) and mapped to ProblemDetails by the global exception handler. FluentValidation deleted.

### Auth — `/api/v1/auth`
| Method | Route | Auth | Body → Response |
|---|---|---|---|
| POST | `/register` | anon | `{email, password}` → 201 `{userId}` |
| POST | `/login` | anon | `{email, password}` → 200 `{accessToken, expiresAtUtc, email, roles[]}` |
| GET | `/me` | authed | → 200 `{userId, email, roles[]}` |

### Products — `/api/v1/products`
| Method | Route | Auth | Notes |
|---|---|---|---|
| GET | `/` | anon | query: `search`, `minPrice`, `maxPrice`, `sortBy(name|price)`, `sortDesc(bool)`, `page≥1`, `pageSize∈[1,100]` → 200 `PagedResult<ProductResponse>` |
| GET | `/{id}` | anon | → 200 / 404 |
| POST | `/` | Admin, Staff | → 201 + Location |
| PUT | `/{id}` | Admin, Staff | → 200 updated / 404 / 400 |
| DELETE | `/{id}` | Admin | → 204 / 404 |

OData deleted (package, EDM config, `[EnableQuery]`). Filtering/sorting/paging composed over projected `IQueryable` server-side; response envelope `{items, page, pageSize, totalCount}`. Response caching retained only on the two anonymous GET product routes (`OutputCache` 30s, no authenticated-route caching — fixes vary-by-user hazard).

### Negotiations — `/api/v1/negotiations`
| Method | Route | Auth | Notes |
|---|---|---|---|
| POST | `/` | Customer | `{productId, proposedPrice}` → 201; 409 if open negotiation exists for pair; 400 over-limit |
| GET | `/mine` | Customer | own negotiations, paged |
| GET | `/` | Admin, Staff | all, paged |
| GET | `/{id}` | owner ∨ Admin ∨ Staff | 404 vs 403 distinction preserved |
| PATCH | `/{id}/proposals` | owner (Customer) | `{proposedPrice}` → 200 `{outcome, proposalsRemaining, currentOffer}`; outcome ∈ `counter_proposed \| auto_rejected`; 409 when closed/no-budget |
| POST | `/{id}/accept` | Admin, Staff | → 200 terminal state |
| POST | `/{id}/decline` | Admin, Staff | → 200 terminal-or-open state |
| DELETE | `/{id}` | owner ∨ Admin | withdraw/delete → 204 |

`GET /me` and ownership resolution replace anonymous-claims guessing; `HttpExecutionContext` crash fixed by safe claim parsing returning null for anonymous.

---

## 7. Cross-Cutting Platform

### Observability
- Serilog: console + rolling file; Loki sink becomes **opt-in** (enabled only when config section present). Request correlation comes from OpenTelemetry trace context instead of the CorrelationId enricher package.
- **OpenTelemetry** added: ASP.NET Core + EF Core + runtime instrumentation, OTLP exporter activated by standard `OTEL_*` env vars; disabled by default locally.

### Resilience & safety
- Rate limiting (.NET built-in): fixed window 10 req/min per IP on `/auth/*`; global concurrency limiter sane defaults.
- CORS: policy from config (`Cors:AllowedOrigins`); empty ⇒ no cross-origin allowed; Development defaults add localhost origins.
- Health checks: `/health/live` (self), `/health/ready` (Npgsql DB connectivity) via built-in EF health check. AspNetCore.HealthChecks.UI trio of packages deleted (dashboard YAGNI).

### Packaging & CI
- Multi-stage `Dockerfile` (build → test → runtime, non-root user, healthcheck hitting `/health/live`).
- `docker-compose.yml`: `api` + `postgres:17-alpine` volume-backed; env-driven config incl. seed credentials.
- `.dockerignore`, `.gitignore` additions (`artifacts/`, `logs/`).
- `.github/workflows/ci.yml`: checkout → setup-dotnet 10 (NuGet cache) → `dotnet restore` → `dotnet format --verify-no-changes` → `dotnet build -c Release` → `dotnet test --collect coverage` → upload OpenAPI doc artifact.
- `dependabot.yml`: nuget + github-actions ecosystems, weekly.
- OpenAPI doc generation moves to `artifacts/openapi/` (gitignored; CI artifact).

### Configuration schema (options pattern, custom `IValidateOptions<T>` + `ValidateOnStart`)
```
Jwt:{Issuer, Audience, SecretKey, ExpiryMinutes}
Database:ConnectionString
Cors:AllowedOrigins[]
Seeding:{AdminEmail, AdminPassword, StaffEmail, StaffPassword, SeedSampleProducts}
Loki:{Url, ...}   (optional presence enables sink)
```
All committed secrets removed from appsettings*.json; appsettings contains only structural defaults.

---

## 8. Testing Strategy

### UnitTests (fast, no host, NSubstitute + Bogus)
- Negotiation lifecycle exhaustive: start (valid/over-limit), counter-propose happy path, auto-reject boundary (=limit passes, just-over rejects), budget exhaustion, accept/decline transitions, operations on terminal states.
- Product create/update rules incl. no-op guard; Price VO invariants; factories; policy boundaries.
- Application services with substituted repos: result mapping, caller-context ownership branches, paging math.
- JWT token generator: claims/expiry shape (fixed TimeProvider).
- Plain xUnit asserts (FluentAssertions removed).

### IntegrationTests (WebApplicationFactory + Testcontainers Postgres, Refit clients)
- Real end-to-end auth: register → login (real JWT, no test scheme) → /me. Bad-credentials and lockout paths.
- Products CRUD full role matrix (anon/customer/staff/admin × 5 routes) incl. validation failures, 404s, filter/sort/page correctness.
- Negotiations: create→counter×2→accept; decline→counter→exhaustion→409; auto-reject >2×; double-open conflict; withdraw; cross-user access matrix; concurrency 409 (update stale product).
- ProblemDetails `code` assertions on every error path.
- Factory resets schema between test classes (migrations applied once per collection fixture).

---

## 9. Documentation & Hygiene
- README rewritten: stack, quickstart (compose + user-secrets), endpoint table, negotiation rules, config reference, default seeded accounts (env-configurable, documented for local compose only).
- `.http` file rewritten with real routes; malformed XML docs fixed; PL/EN mixed messages unified to English; typo "appliable" fixed.
- Repo cleanup commit: delete empty dirs/artifacts/logs.

---

## 10. Package Manifest Changes

**Removed:** `Microsoft.AspNetCore.OData`, `FluentValidation.*`, `AspNetCore.HealthChecks.UI*` (×3), `Microsoft.AspNetCore.Components.QuickGrid.EntityFrameworkAdapter`, `Microsoft.VisualStudio.Web.CodeGeneration.Design`, `NuGet.Common`, `NuGet.Protocol`, `Microsoft.EntityFrameworkCore.InMemory`, `Serilog.Enrichers.CorrelationId` (replaced by OTel trace correlation).

**Kept deliberately:** `System.IdentityModel.Tokens.Jwt` in Infrastructure — explicit reference beats transitive for security-sensitive token minting.

**Added:** `Npgsql.EntityFrameworkCore.PostgreSQL`, `EFCore.NamingConventions`, `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.EntityFrameworkCore`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `Testcontainers.PostgreSql` (tests).

**Unchanged:** Vogen, Serilog.AspNetCore, Serilog.Sinks.Grafana.Loki (opt-in), Scalar.AspNetCore, xunit.v3, NSubstitute, Bogus, coverlet, SonarAnalyzer + Meziantou analyzers, Refit (tests), Microsoft.AspNetCore.Authentication.JwtBearer, Identity.EntityFrameworkCore, Microsoft.AspNetCore.OpenApi, MVC.Testing.

---

## 11. Implementation Phases (order matters)
1. **Cleanup & structure** — delete dead projects/dirs/artifacts/deps; move IDs into Domain; fix csproj graph; solution builds green.
2. **Domain rebuild** — Price VO consolidation, status enum, explicit lifecycle methods + outcomes, policy relocation; unit tests for lifecycle.
3. **Application rebuild** — feature-scoped services with CallerContext, ports, result unions; delete obsolete flows; unit tests.
4. **Infrastructure rebuild** — AppDbContext + Npgsql configurations, partial index, xmin versions, repositories, hardened JWT manager + options validation, migrations, async seeding.
5. **Api rebuild** — minimal-API modules, validation, ProblemDetails mapping, policies, rate limiting, CORS, output caching, health; Program.cs split.
6. **Platform** — Dockerfile, compose, CI workflow, dependabot, OTel wiring, gitignore/OpenAPI artifact moves.
7. **Integration tests** — Testcontainers factory + Refit clients; full matrices from §8.
8. **Docs & final sweep** — README, .http, analyzer-clean build, `dotnet format`, full test run.

Each phase ends with: build zero-warnings-as-errors green + relevant tests passing.

## 12. Risks & Mitigations
- **Behavior change risk** (proposal semantics now total-of-3 including initial): documented explicitly in spec + README; matches README's "propose a price for 3 times".
- **Testcontainers requires Docker in CI**: GH Actions `ubuntu-latest` provides Docker natively.
- **Vogen EF converter naming**: verified convention `<VoName>EfCoreValueConverter`; fallback is manual `HasConversion` lambdas in Infrastructure.
- **Identity + snake_case naming**: Identity tables explicitly re-mapped to their conventional names to avoid breaking UserManager SQL expectations (configurations pin table names).

