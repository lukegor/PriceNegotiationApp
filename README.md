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
| Tests | xUnit v3, NSubstitute, Bogus, Testcontainers (real Postgres), 50+ tests |
| Platform | Docker multi-stage image, docker-compose, GitHub Actions CI, Dependabot |

## Architecture

Modular monolith: three bounded contexts behind compiler-enforced boundaries,
one PostgreSQL schema per context.

```
src/
  PriceNegotiationApp.AppHost               composition root: pipeline, authN/authZ validation,
  │                                         ProblemDetails, rate limiting, CORS, output caching,
  │                                         health checks, OTel; wires modules and the single
  │                                         inter-module adapter
  PriceNegotiationApp.BuildingBlocks        shared primitives: CallerContext, paging,
  │                                         error semantics, policy names, role names
  PriceNegotiationApp.Modules.Identity      users/roles/JWT issuance/seeding → schema identity
  PriceNegotiationApp.Modules.Catalog       products → schema catalog
  PriceNegotiationApp.Modules.Negotiations  negotiations/customers/policy → schema negotiations

tests/
  PriceNegotiationApp.Modules.*.Tests       per-module unit tests (module public surface only)
  PriceNegotiationApp.IntegrationTests      WebApplicationFactory + Testcontainers PostgreSQL
```

Rules: modules never reference each other; cross-module interaction flows through
consumer-owned ports wired in AppHost (`Composition/CatalogToNegotiations` is currently
the only edge). Each context owns its migrations; startup applies them in order
identity → catalog → negotiations. Per-module connection overrides:
`Database:Modules:{Identity|Catalog|Negotiations}:ConnectionString` (falls back to
`Database:ConnectionString`).

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
dotnet user-secrets set "Seeding:AdminPassword" "Admin123!" --project src/PriceNegotiationApp.Api
dotnet user-secrets set "Seeding:StaffPassword" "Staff123!" --project src/PriceNegotiationApp.Api

dotnet run --project src/PriceNegotiationApp.Api
```

Swagger UI (Scalar) is available at `/scalar` in Development.

## Negotiation rules

1. A customer opens a negotiation on a product with an initial proposal — this consumes
   proposal 1 of 3.
2. Staff **accept** (terminal `Accepted`) or **decline**. Declining keeps the negotiation
   open so the customer can counter with a remaining proposal.
3. A counter-proposal above 2× the snapshotted base price immediately closes the
   negotiation as `Declined` (auto-rejection).
4. When the proposal budget is spent, further counter-proposals are refused (`409`).
5. The owner can withdraw an open negotiation at any time; admins may delete any.
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
- `GET /health/ready` — database connectivity
- OpenTelemetry traces/metrics export via standard `OTEL_*` environment variables.

## CI

GitHub Actions runs restore → `dotnet format` check → Release build → unit tests →
Testcontainers-based integration tests on every push and pull request.

## License

Apache License 2.0
