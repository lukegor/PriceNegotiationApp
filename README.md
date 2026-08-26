# PriceNegotiationApp

Backend-only ASP.NET Core Web API that lets customers negotiate prices with shop staff.
Customers register, browse products and open price negotiations; staff review offers and
accept or decline them. A negotiation allows up to **3 proposals in total** (the initial
offer plus two counters). Any proposal above **2× the product's base price** is auto-rejected.

## Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10, C# (nullable + warnings-as-errors) |
| API | ASP.NET Core minimal APIs, built-in request validation |
| Domain | Vogen value objects, business-rule pattern |
| Persistence | EF Core 10 + Npgsql (PostgreSQL 17), snake_case schema, xmin concurrency |
| Identity | ASP.NET Core Identity + JWT Bearer (strict issuer/audience/lifetime validation) |
| Observability | Serilog (console/file), OpenTelemetry (OTLP), `/health/live`, `/health/ready` |
| Tests | xUnit v3 on Microsoft.Testing.Platform (MTP code coverage), Bogus, Shouldly, ArchUnitNET boundary tests, Testcontainers (real Postgres) |
| Platform | Docker multi-stage image, docker-compose, GitHub Actions CI, Dependabot |

## Architecture

Modular monolith: three bounded contexts behind compiler-enforced boundaries,
one PostgreSQL schema per context.

```
src/
  PriceNegotiationApp.Api                   composition root: pipeline, authN/authZ validation,
  │                                         ProblemDetails, rate limiting, CORS, output caching,
  │                                         health checks, OTel; wires modules and the single
  │                                         inter-module adapter
  PriceNegotiationApp.SharedKernel          shared primitives: CallerContext, paging,
  │                                         error semantics, policy names, role names,
  │                                         seeding/design-time factory bases
  PriceNegotiationApp.Modules.Identity      users/roles/JWT issuance/seeding → schema identity
  PriceNegotiationApp.Modules.Catalog       products → schema catalog
  PriceNegotiationApp.Modules.Negotiations  negotiations/customers/policy → schema negotiations

tests/
  PriceNegotiationApp.ArchitectureTests     ArchUnitNET rules pinning module boundaries
  PriceNegotiationApp.Modules.*.Tests       per-module unit tests
  PriceNegotiationApp.IntegrationTests      WebApplicationFactory + Testcontainers PostgreSQL
```

Every module uses the same layout:

```
Modules.X/
  XModule.cs, XEndpoints.cs   public registration + endpoint mapping
  Domain/                     entities, value objects, policies        (internal)
  Features/<Entity>/          handlers + models per feature group       (internal)
  Persistence/                DbContext, configurations, migrations     (internal)
  Ports/                      required-services contracts               (public)
  Public/                     cross-assembly contract surface           (public)
  Seeding/                    module seeder on the shared base          (internal)
```

Rules:

- Modules never reference each other; module implementation types are `internal`, so the
  boundary is enforced at compile time. `InternalsVisibleTo` is granted only to the
  composition root (`Api`) and each module's own test project.
- Cross-module interaction flows through consumer-owned ports wired in Api
  (`Composition/CatalogToNegotiations` is currently the only edge).
- Each context owns its migrations; startup applies them in order
  identity → catalog → negotiations. Per-module connection overrides:
  `Database:Modules:{Identity|Catalog|Negotiations}:ConnectionString` (falls back to
  `Database:ConnectionString`).

### Tactical DDD laws

- Endpoints are transport adapters: routing, auth attributes and status shaping only.
  Application logic lives in per-operation `*Handler` services under `Features/`.
- Module `DbContext`s are the unit of work; `DbSet<T>` is the aggregate's collection.
  No repository/UoW abstractions (enforced by an architecture test).
- Cross-aggregate invariants live at the persistence boundary (partial unique indexes)
  with endpoint fast-paths for friendly errors — never inside a single aggregate.
- Negotiation policy values are snapshotted onto the aggregate at creation; config changes
  never rewrite in-flight negotiations.
- Domain/integration events are intentionally absent until the first real subscriber
  (deal-on-accept / notifications features). The pattern is pre-defined in
  `docs/superpowers/specs/2026-08-25-ddd-audit-design.md` §F-04 and lands with that feature.
- Money inside aggregates uses value objects; ratios/multipliers use plain decimals.

### Migrations

Each module owns its migration stream (history tables live in the default schema):

```bash
dotnet ef migrations add <Name> --context CatalogDbContext `
  -p src/PriceNegotiationApp.Modules.Catalog -o Persistence/Migrations
```
## Quickstart

### Docker Compose (recommended)

```bash
cp .env.example .env        # then edit the values
docker compose up --build
```

The API listens on http://localhost:8080. Migrations run automatically on startup.

### Local .NET run

```bash
dotnet user-secrets set "Jwt:SecretKey" "dev-only-secret-key-change-me-32-chars-min!!" --project src/PriceNegotiationApp.Api
dotnet user-secrets set "Jwt:Issuer"      "https://localhost:5185" --project src/PriceNegotiationApp.Api
dotnet user-secrets set "Jwt:Audience"    "price-negotiation-api" --project src/PriceNegotiationApp.Api
dotnet user-secrets set "Database:ConnectionString" "Host=localhost;Port=5432;Database=pricenego_dev;Username=postgres;Password=postgres" --project src/PriceNegotiationApp.Api
dotnet user-secrets set "Seeding:AdminPassword" "<your own random 12+ char mixed secret>" --project src/PriceNegotiationApp.Api
dotnet user-secrets set "Seeding:StaffPassword" "<your own random 12+ char mixed secret>" --project src/PriceNegotiationApp.Api

dotnet run --project src/PriceNegotiationApp.Api
```

Swagger UI (Scalar) is available at `/scalar` in Development.

## Negotiation rules

1. A customer opens a negotiation on a product with an initial proposal — this consumes
   proposal 1 of 3.
2. Staff **accept** (terminal `Accepted`) or **reject the current offer** (`POST .../decline`);
   rejecting keeps the negotiation open so the customer can spend a remaining proposal and
   does not consume budget.
3. A counter-proposal above the snapshotted offer-multiplier limit (default 2× base price,
   frozen at creation time) immediately closes the negotiation as terminal `Rejected`
   (auto-rejection).
4. When the snapshotted proposal budget is spent, further counter-proposals are refused (`409`).
5. The owner can withdraw an open negotiation at any time — this soft-closes it as terminal
   `Withdrawn` and preserves history; only admins hard-delete rows.
6. Deleting a product does not delete or block its negotiations — they keep their
   price snapshot (product existence is only validated when a negotiation is created).

## API surface (v1)

| Method | Route | Access |
|---|---|---|
| POST | `/api/v1/auth/register` | anonymous (rate-limited) |
| POST | `/api/v1/auth/login` | anonymous (rate-limited) |
| GET | `/api/v1/auth/me` | authenticated |
| GET | `/api/v1/products?search=&minPrice=&maxPrice=&sortBy=&sortDesc=&page=&pageSize=` | anonymous |
| GET | `/api/v1/products/{id}` | anonymous |
| POST | `/api/v1/products` | Admin, Staff |
| PUT | `/api/v1/products/{id}` | Admin, Staff |
| DELETE | `/api/v1/products/{id}` | Admin |
| POST | `/api/v1/negotiations` | Customer |
| GET | `/api/v1/negotiations/mine` | Customer |
| GET | `/api/v1/negotiations` | Admin, Staff |
| GET | `/api/v1/negotiations/{id}` | owner, Admin, Staff |
| PATCH | `/api/v1/negotiations/{id}/proposals` | owner |
| POST | `/api/v1/negotiations/{id}/accept` | Admin, Staff |
| POST | `/api/v1/negotiations/{id}/decline` | Admin, Staff |
| DELETE | `/api/v1/negotiations/{id}` | owner or Admin |

Status vocabulary: `Open | Accepted | Rejected | Withdrawn`. `Rejected` is terminal
auto-rejection; staff decline responses carry `"outcome":"current_offer_rejected"`
while the status stays `Open`.

Errors use RFC 7807 ProblemDetails with a stable machine-readable `code` extension
(e.g. `product_not_found`, `negotiation_already_open`, `no_proposals_remaining`).

## Configuration

| Key | Purpose |
|---|---|
| `Database:ConnectionString` | PostgreSQL connection string |
| `Jwt:Issuer` / `Jwt:Audience` / `Jwt:SecretKey` (≥32 chars) / `Jwt:ExpiryMinutes` | token settings — validated at startup |
| `Seeding:{AdminEmail,AdminPassword,StaffEmail,StaffPassword,SeedSampleProducts}` | startup seed data |
| `RateLimiting:AuthPermitLimit` | per-IP requests/minute on auth endpoints (default 30) |
| `Cors:AllowedOrigins` | cross-origin allow-list |

No secrets are committed: use user-secrets locally and environment variables in production.

## Health & telemetry

- `GET /health/live` — process liveness
- `GET /health/ready` — database connectivity (JSON body names each dependency)
- OpenTelemetry traces/metrics export via standard `OTEL_*` environment variables.

### Local telemetry dashboard

```bash
docker compose -f docker-compose.yml -f compose.observability.yml up --build
```

The dashboard is **token-protected** (it displays request payloads and logs), and its
telemetry ingestion endpoint requires an **API key** — untrusted apps cannot inject or
spoof telemetry. Login at http://127.0.0.1:18888/login?t=<ASPIRE_DASHBOARD_TOKEN>; both
values live in your `.env`. For `dotnet run` development, start just the dashboard
(`docker compose -f docker-compose.yml -f compose.observability.yml up aspire-dashboard`)
and set the user secrets `OTEL_EXPORTER_OTLP_ENDPOINT` (`http://localhost:18889`) and
`OTEL_EXPORTER_OTLP_HEADERS` (`x-otlp-api-key=<ASPIRE_OTLP_API_KEY>`).

## Testing

```bash
dotnet test --solution PriceNegotiationApp.slnx                        # everything (Docker needed)
dotnet test --project tests/PriceNegotiationApp.Modules.Catalog.Tests  # one project
```

Every run also writes `TestResults/*.trx` and `TestResults/*.cobertura.xml`.
Generated test data comes from Bogus through a shared `TestKit`:

- Data is deterministic per call site — re-running the same command replays it.
- A failure prints a `fuzz run-seed=…` banner plus the arranged values; replay it with:

```bash
$env:TEST_SEED='<seed from the failure>'; dotnet test --filter <same filter>
```

## CI

GitHub Actions runs restore → `dotnet format` check → Release build → unit tests →
Testcontainers-based integration tests on every push and pull request.

## License

Apache License 2.0
