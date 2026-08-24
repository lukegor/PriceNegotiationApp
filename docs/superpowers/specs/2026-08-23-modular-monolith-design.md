# PriceNegotiationApp — Modular Monolith Design

Date: 2026-08-23
Status: Proposed
Mandate: Restructure the solution into a true modular monolith with one DbContext and one database schema per module, enforcing module boundaries at the compiler level. Greenfield evaluation of all existing abstractions.

---

## 1. Current-State Audit

The codebase already completed a modernization pass (see `2026-08-23-modernization-design.md`): 4 layered projects (`Domain → Application → Infrastructure → Api`), PostgreSQL + EF Core 10, hardened JWT, minimal APIs, Testcontainers CI. That baseline is solid. This design evaluates it with fresh eyes and finds the following structural weaknesses:

| # | Finding | Consequence |
|---|---|---|
| F1 | **One `AppDbContext` owns everything** — Identity tables, products, negotiations, customers | Every module's schema changes collide in one migration stream; nothing prevents a negotiation query from joining `products`; modules cannot evolve or be extracted independently |
| F2 | **Layer projects are not boundaries** — `Application` references `Microsoft.EntityFrameworkCore` directly (`NegotiationService` composes `IQueryable` with `LongCountAsync/Skip/Take`) | The "ports" are leaky: repository abstractions expose EF-specific queryables; swapping persistence would break every service |
| F3 | **Repository + UnitOfWork wrappers add ceremony without value** — `UnitOfWork` only forwards `SaveChangesAsync` and translates one exception; repositories forward `Set<T>()` calls | Boilerplate that must be read, maintained, and mocked; EF Core *is* already a unit-of-work + repository over the mapped model |
| F4 | **Feature logic is split across four assemblies** — a single endpoint's code lives in `Api/Modules`, `Application/Features`, `Application/Abstractions`, `Infrastructure/Persistence` | High cost of change per feature; no ownership cohesion; navigation requires jumping between layers even for trivial edits |
| F5 | **Cross-module data need is invisible** — negotiations snapshot the product price at creation via the shared `IProductRepository`; the coupling point is unmarked and unpoliced | Nothing stops future code from adding live joins to products inside negotiation queries |
| F6 | Hygiene: root-level stale project folders (bin/obj only), `.env` tracked in git (placeholder values today, but one `git add .` away from leaking real secrets) | Repo noise; secret-leak hazard |

### What is worth keeping

- The domain logic itself: `Negotiation` state machine (proposal budget, auto-reject multiplier, base-price snapshot), policy abstraction, Vogen IDs, xmin optimistic concurrency.
- Operational hardening: strict JWT validation, ProblemDetails + stable error codes, rate limiting, health checks, OTel, Testcontainers integration suite, warnings-as-errors, central package management.
- The API contract (routes, status semantics 400/422/409) — unchanged by this design.

---

## 2. Goals & Non-Goals

### Goals
1. Three genuine bounded contexts — **Identity**, **Catalog**, **Negotiations** — each a self-contained project owning its domain, features, endpoints, and persistence.
2. **One DbContext + one Postgres schema per module** (`identity`, `catalog`, `negotiations`), with independent migration streams and per-module connection-string override.
3. Compiler-enforced boundaries: modules reference only `BuildingBlocks`, never each other; inter-module interaction exclusively through consumer-defined ports wired in the composition root.
4. Delete ceremony that fails the greenfield test: generic repositories, forwarding UnitOfWork, cross-layer IQueryable leaks, single-use business-rule classes.
5. Keep routes, payloads, status-code semantics, and business rules compatible (verified by the existing integration suite) — with exactly one pinned semantic change, documented in §6 (product-delete vs negotiation history).
6. Fix hygiene items from the audit.

### Non-Goals
- Microservices, message broker, outbox/inbox, event sourcing — YAGNI at this scale; the design keeps an extraction path open instead of pre-building it.
- Changing any HTTP route, payload, status-code semantic, or business rule.
- Frontend client.
- Multi-database deployment (all three schemas live in one Postgres database by default).

---

## 3. Considered Approaches

### A. Split contexts inside the existing layered projects (minimal)
Keep `Domain/Application/Infrastructure/Api`; replace `AppDbContext` with three contexts in `Infrastructure`.
- ✔ Cheapest (~2 phases).
- ✘ Fake modularity: all contexts share one assembly, so any code can touch any context; F2–F4 remain; "separate DbContexts" becomes a naming convention rather than a boundary.

### B. Modular monolith — vertical module projects + schema/context per module (**recommended**)
Three self-contained module projects, a thin host, a tiny shared kernel. Contexts, schemas, and migrations follow the project boundaries.
- ✔ Real boundaries enforced by project references; feature cohesion (one place per feature); independent migrations; honest extraction path (point a module's connection string at another DB later); reads naturally match the bounded contexts.
- ✘ More projects (7 vs 6), one-time restructuring cost, multi-context EF tooling discipline required.

### C. Full DDD modular monolith with integration-event bus + inbox/outbox (à la enterprise templates)
- ✘ One inter-module interaction exists in the whole system, and it is synchronous-by-nature (price snapshot at creation). An event bus here is pure infrastructure tax. Rejected.

**Decision: B.** The app has exactly three contexts with distinct lifecycles (auth/security churn vs catalog CRUD vs negotiation state machine) and a single, well-defined cross-context dependency — precisely the shape modular monoliths exist for.

---

## 4. Target Solution Structure

```
PriceNegotiationApp.slnx
src/
  PriceNegotiationApp.AppHost/              composition root (renamed from Api):
  │                                         pipeline, authN/authZ validation, ProblemDetails,
  │                                         rate limiting, CORS, output cache, health checks,
  │                                         OTel, Scalar; wires module registration + adapters
  PriceNegotiationApp.BuildingBlocks/       ~10 small stable types, zero domain logic:
  │                                         CallerContext, PageQuery, PagedResult, shared
  │                                         exception types + ProblemDetails mapping helpers,
  │                                         shared error codes (unauthorized, forbidden,
  │                                         validation_failed, concurrency_conflict)
  PriceNegotiationApp.Modules.Identity/
  │   IdentityModule.cs                     AddIdentityModule(cfg) / MapAuthEndpoints(ep)
  │   Features/Auth/                        Register, Login, Me — endpoint + handler +
  │   │                                     request/response records colocated per operation
  │   Auth/JwtManager.cs                    token issuance (module-private)
  │   Persistence/                          IdentityModuleDbContext (schema "identity"),
  │                                         Configurations/, Migrations/, design-time factory
  │   Seeding/                              roles + admin/staff accounts (config-driven)
  │   Public/UserRoles.cs                   role constants consumed by AppHost policies
  PriceNegotiationApp.Modules.Catalog/
  │   CatalogModule.cs                      AddCatalogModule(cfg) / MapCatalogEndpoints(ep)
  │   Features/Products/                    List, Get, Create, Update, Delete slices
  │   Domain/Product.cs                     entity + guards + module-local Price invariant
  │   Persistence/                          CatalogDbContext (schema "catalog"), Migrations/
  │   Seeding/                              sample products (Development only)
  PriceNegotiationApp.Modules.Negotiations/
      NegotiationsModule.cs                 AddNegotiationsModule(cfg) / MapNegotiationEndpoints(ep)
      Features/Negotiations/                Create, List(Mine|All), Get, CounterPropose,
      │                                     Accept, Decline, Withdraw slices
      Domain/                               Negotiation state machine, Customer,
      │                                     INegotiationPolicy + DefaultNegotiationPolicy,
      │                                     module-local Price invariant, Vogen IDs
      Ports/IProductPriceProvider.cs        the ONE cross-module port (consumer-owned)
      Persistence/                          NegotiationsDbContext (schema "negotiations"),
                                            Migrations/
tests/
  PriceNegotiationApp.Modules.Identity.Tests/     JwtManager claims/expiry, lockout config
  PriceNegotiationApp.Modules.Catalog.Tests/      product rules, update idempotency
  PriceNegotiationApp.Modules.Negotiations.Tests/ lifecycle matrix, policy boundaries, Price VO
  PriceNegotiationApp.IntegrationTests/           WebApplicationFactory + Testcontainers PG
                                                  (harness unchanged; matrices extended)
```

### Dependency rules (enforced by csproj references)

```
BuildingBlocks            → BCL + DI/logging abstractions only
Modules.{Identity,Catalog,Negotiations} → BuildingBlocks (+ their NuGet packages). NEVER another module.
AppHost                   → all three modules (registration + adapters only)
```

Any PR introducing a module-to-module project reference should fail review by rule; the architecture is auditable by reading five `.csproj` files.

### Module anatomy convention

Inside a module there are no internal layer names — the module *is* the boundary. Layout is feature-first:

```
Features/<Entity>/<Operation>.cs   // route group declaration + handler + DTOs together
Domain/                            // entities, value objects, pure policy — no I/O
Persistence/                       // DbContext, configurations, migrations
<Name>Module.cs                    // AddXxx(IConfiguration), MapXxx(IEndpointRouteBuilder)
Public/                            // only what other assemblies legitimately consume
```

Handlers take their module's `DbContext` directly. No repository interfaces, no IUnitOfWork (rationale: §7).

---

## 5. Persistence Design

### Contexts & schemas

| Context | Project | Schema | Tables |
|---|---|---|---|
| `IdentityModuleDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>` | Modules.Identity | `identity` | users, roles, user_roles, user_claims, user_roles claims/logins/tokens (pinned snake_case names — carried over unchanged) |
| `CatalogDbContext` | Modules.Catalog | `catalog` | products |
| `NegotiationsDbContext` | Modules.Negotiations | `negotiations` | negotiations, customers |

Each context: `UseNpgsql(...)` + `UseSnakeCaseNamingConvention()` + `HasDefaultSchema(...)`. `ApplyConfigurationsFromAssembly(<this context>.Assembly)` now resolves per-module automatically — a side benefit: configurations physically cannot leak across contexts.

**No cross-schema foreign keys.** Logical references are plain columns:
- `negotiations.product_id` (Guid) — no FK to `catalog.products`
- `negotiations.customer_id` → FK within schema to `customers`
- `customers.identity_user_id` (Guid, unique index) — no FK to `identity.users`

This is what makes each module's schema independently migratable and extractable. Referential honesty is enforced in handlers (the producer must exist *now*, then is snapshotted — see §6).

Carried over unchanged: xmin-system-column optimistic concurrency on `products` and `negotiations`; partial unique index `(product_id, customer_id) WHERE status = 'Open'`; Guid v7 keys; status stored as int.

### Migrations

- Per-context migration sets in each module's `Persistence/Migrations/`.
- Tooling: `dotnet ef migrations add X --project src/...Modules.Y --context YDbContext` (context flag mandatory — documented in README to prevent foot-guns).
- Startup: one `MigrationHostedService` (AppHost) applies contexts in fixed order **identity → catalog → negotiations**, fail-fast on error. Idempotent by nature.
- Existing deployments: this is a schema rename/relocation. For disposable environments (compose volume, CI) start clean. A one-off SQL copy script (old public tables → new schemas, remapping identity table names) ships under `docs/sql/legacy-data-migration.sql` for any persistent environment.

### Connection strings

```jsonc
"Database": {
  "ConnectionString": "<default for all modules>",
  "Modules": {
    "Identity":     { "ConnectionString": "<optional override>" },
    "Catalog":      { "ConnectionString": "<optional override>" },
    "Negotiations": { "ConnectionString": "<optional override>" }
  }
}
```

Default topology = one database, three schemas. Overrides exist so a module can be moved to its own database (or replaced by a stub) without code change — the seam that makes "monolith first" honest.

### Health & readiness

`AddDbContextCheck<T>()` per context under the `ready` tag; `/health/ready` reports all three. Liveness unchanged.

---

## 6. Inter-Module Communication

There is exactly **one** runtime cross-module interaction: creating a negotiation must verify the product exists and capture its price for the snapshot.

Pattern: **consumer-owned port, host-wired adapter**.

```csharp
// Modules.Negotiations/Ports/IProductPriceProvider.cs
public interface IProductPriceProvider
{
    /// <returns>null when the product does not exist</returns>
    Task<ProductSnapshot?> GetAsync(Guid productId, CancellationToken ct);
}
public readonly record struct ProductSnapshot(Guid ProductId, decimal Price);

// AppHost/Composition/CatalogToNegotiations.cs — the entire integration surface
internal sealed class CatalogToNegotiations(CatalogDbContext db) : IProductPriceProvider
{
    public async Task<ProductSnapshot?> GetAsync(Guid productId, CancellationToken ct) =>
        await db.Products
            .Where(p => p.Id.Value == productId)
            .Select(p => new ProductSnapshot(p.Id.Value, p.Price))
            .FirstOrDefaultAsync(ct);
}
```

Properties:
- Zero module→module references; Negotiations compiles without Catalog existing.
- The adapter is the audit point: grep `Composition/` and you see every inter-module edge.
- Reads project primitives (Guid/decimal) across the boundary — foreign aggregate types never leak.
- Future replacement (cached façade, gRPC client, message-driven replica) touches one file.

Everything else stays strictly intra-module: staff accept/decline, withdraw, listing — none need catalog data because the base-price snapshot already decoupled ongoing negotiations from product rows. `GET /negotiations/{id}` responses intentionally do not join product names (existing behavior, now an explicit architectural property).

**Pinned behavioral decision:** today, `negotiations.product_id` carries a real FK (`Restrict`), so deleting a product that has *any* negotiation history fails at the database level (surfacing as a server error). Under separate schemas that FK must go, and the chosen replacement semantics are: **deleting a product succeeds; its negotiations survive on their price snapshots** (consistent with how closed-negotiation history is already preserved). This is the one intentional behavior change in the refactor; the integration suite pins it with an explicit test, and the README notes it. If the business later prefers delete-blocking when open negotiations exist, it becomes an explicit port call in the catalog delete handler returning `409 negotiation_exists_for_product` — not a hidden FK side effect.

---

## 7. Application-Code Simplification (greenfield deletions)

| Removed | Replacement | Rationale |
|---|---|---|
| `IProductRepository`, `INegotiationRepository`, `ICustomerRepository` + implementations | Handlers use the module `DbContext` directly | EF Core already implements repository/UoW semantics over mapped aggregates; wrappers only forwarded calls and leaked `IQueryable` into Application (F2/F3) |
| `IUnitOfWork` + `UnitOfWork` | `SaveChangesAsync` on the context inside each handler; concurrency translation via a `BuildingBlocks` extension method `SaveChangesWithConflictDetectionAsync(this DbContext)` | One exception-mapping line does not justify an abstraction and a second DI registration per module |
| `IUserAccountStore` | Identity handlers use `UserManager`/`SignInManager` directly (already referenced by the module) | Port existed only to keep ASP.NET types out of the old Application layer; the Identity module legitimately hosts them |
| `IJwtTokenGenerator` (public port) | `JwtManager` stays, becomes module-private | Sole consumer is the login handler inside the same module |
| `IBusinessRule` / `Entity.CheckRule` pattern + two single-use rule classes | Inline guard methods on entities | Two single-use classes validating name length is ceremony, not modeling; domain exceptions (which remain) carry the same semantics |
| `Application` project, `Domain` project, `Infrastructure` project, old `Api` project | Distributed into modules as per §4 | F4: feature cohesion beats technical layering at this scale |
| Root stale project folders; `logs/` artifacts | Deleted; `.env` gitignored (`.env.example` remains tracked) | F6 |

What deliberately survives:
- **Vogen** ID value objects — cheap type safety; IDs become module-private except where a port signature needs them (none currently — ports speak `Guid`).
- **`INegotiationPolicy`/`DefaultNegotiationPolicy`** — pure domain logic, injected into `Negotiation.Start/CounterPropose`; unit-testable without I/O.
- **Domain exceptions + global exception handler + stable ProblemDetails `code` values** — the error contract is frozen; per-entity codes (`product_not_found`, …) move next to their features, generic ones stay in BuildingBlocks.
- **CallerContext** resolved in AppHost from `ClaimsPrincipal`, passed to handlers as a plain record.

---

## 8. Host Composition (AppHost)

`Program.cs` remains thin; composition order makes the system legible:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog(/* unchanged */);

builder.Services
    .AddBuildingBlocks()                                   // ProblemDetails + exception handler
    .AddIdentityModule(builder.Configuration)              // context, Identity core, JWT issuance, seeding
    .AddCatalogModule(builder.Configuration)               // context, output-cache usage, dev seeder
    .AddNegotiationsModule(builder.Configuration)          // context, policy singleton
    .AddSingleton<IProductPriceProvider, CatalogToNegotiations>();  // the adapter seam

builder.Services.AddJwtAuthentication(builder.Configuration);   // validation only (issuer/audience/lifetime/key)
// rate limiting ("auth" policy), CORS, output cache ("short"), health checks ×3,
// OpenAPI + Scalar, OTel — all unchanged, host-owned

var app = builder.Build();
app.UsePipeline();
app.MapAuthEndpoints();
app.MapCatalogEndpoints();
app.MapNegotiationEndpoints();
await app.RunAsync();
```

Endpoint groups keep their existing routes, auth requirements, rate-limit (`auth` policy on register/login), and cache (`short` policy on anonymous product GETs) declarations — only the file they live in moves.

---

## 9. Testing Strategy

**Per-module unit test projects are part of the boundary enforcement**: a module's tests can only reach the module's public surface plus its internals via project reference — which is fine, since tests ship with the module. What they may not do is reference another module; the reference graph proves it.

- **Negotiations.Tests**: exhaustive lifecycle matrix (start valid/over-limit; counter happy path; auto-reject at ±ε of `base × multiplier`; budget exhaustion; accept/decline/withdraw transitions; operations on terminal states), policy boundaries, Price invariant, remaining-proposal math. Pure domain — no mocks needed after the repository deletion; NSubstitute shrinks to near-zero.
- **Catalog.Tests**: create/update rules, idempotent PUT no-op guard, decimal precision.
- **Identity.Tests**: JwtManager claim shape/expiry with `FakeTimeProvider`, options validator rejection cases.
- **IntegrationTests**: harness untouched (WebApplicationFactory + Testcontainers PG, `UseSetting` overrides gain per-module connection defaults implicitly). Suites re-pointed at unchanged routes. Additions:
  - migration ordering smoke (fresh container → all three schemas present);
  - `/health/ready` aggregates three DB checks;
  - product deleted ⇒ negotiation survives with snapshot (§6);
  - existing RBAC/validation/paging/lifecycle/error-code matrices pass byte-identically — the regression gate for the whole refactor.

CI pipeline steps are unchanged (restore → format → build → unit → integration); only project discovery broadens automatically.

---

## 10. Configuration Schema (delta only)

All existing keys unchanged. Added:

| Key | Purpose |
|---|---|
| `Database:Modules:{Identity,Catalog,Negotiations}:ConnectionString` | optional per-module override; falls back to `Database:ConnectionString` |

`.env.example` gains nothing (compose keeps single DB). `.env` becomes gitignored.

---

## 11. Implementation Plan

Sequencing principle: **split the data layer first inside known-good structure, then move code** — EF tooling risk and structural-move risk never land in the same phase. Every phase ends with zero-warning build + green tests.

| Phase | Scope | Verify | Size |
|---|---|---|---|
| **0. Hygiene** | Delete stale root project folders; gitignore `.env` + `logs/`; confirm slnx untouched | `git status` clean-ish; build green | S |
| **1. Contexts split (in place)** | Inside current `Infrastructure`: create `IdentityModuleDbContext` (schema-pinned Identity tables), `CatalogDbContext`, `NegotiationsDbContext` with `HasDefaultSchema`; regenerate three migration sets; repos take their context; `MigrationHostedService` applies ordered; health checks ×3; per-connection-string plumbing | Full integration suite vs migrated-fresh DB + legacy-copy script smoke | M |
| **2. BuildingBlocks** | New project; move `CallerContext`, `PageQuery`, `PagedResult`, shared exceptions + codes, `SaveChangesWithConflictDetectionAsync`; all projects reference it | Build green; unit tests move compile | S |
| **3. Negotiations module carve-out** | New project; move Negotiation/Customer/policy/IDs + negotiation endpoints; introduce `IProductPriceProvider` port + temporary adapter over legacy context; delete negotiation repos; move lifecycle unit tests into module test project | Negotiations suites green end-to-end | M |
| **4. Catalog module carve-out** | Same motion for Product + product endpoints + dev seeding + `Catalog.Tests`; swap §6 adapter onto `CatalogDbContext` | Products matrix + cross-module snapshot tests green | M |
| **5. Identity module carve-out** | Move ApplicationUser, Identity store wiring (UserManager direct), JwtManager + options validator, auth endpoints + seeding + `Identity.Tests`; auth rate-limit declarations ride along | Auth flow + lockout tests green | M |
| **6. Legacy deletion + rename** | Delete `Domain/Application/Infrastructure/Api` projects; rename `Api→AppHost` (root namespace, OTel service name, Dockerfile paths, slnx); final sln graph check (5 src + 4 test projects) | Clean build from scratch; `dotnet format` | S |
| **7. Docs & polish** | README architecture section (modular diagram, multi-context EF cheat-sheet, config delta), `.http` sanity pass, legacy SQL script finalized | CI green end-to-end | S |

Critical path: 1 → 3 → 4; phases 2 and 5 parallelize off it. Estimated total ≈ 3–5 focused sessions.

### Definition of done
- [ ] `grep -r "DbContext" src --include=*.csproj -l` shows each context in exactly one module project.
- [ ] No module `.csproj` references another module.
- [ ] `dotnet ef migrations list --context X` shows an independent stream per module.
- [ ] Integration suite passes unchanged, plus the four additions in §9.
- [ ] No `IRepository`/`IUnitOfWork` symbols remain; Application/EF leakage impossible (project gone).
- [ ] `.env` untracked; stale folders gone; warnings-as-errors build green.

---

## 12. Risks & Mitigations

| Risk | Mitigation |
|---|---|
| Multi-context EF tooling mistakes (migrations landing in wrong context/folder) | Mandatory `--context` flag documented; per-project migrations folder asserted in review; Phase 1 isolates all tooling risk before any code moves |
| Identity table remap regressions (UserManager raw SQL expectations) | Pinned snake_case mappings carried over verbatim; auth integration suite exercises register/login/lockout against the remapped schema before any structural move |
| Data loss on schema relocation for persistent envs | Ship `docs/sql/legacy-data-migration.sql`; compose/CI default to fresh volume (documented) |
| Seeding double-execution across three hosted services | Seeders stay idempotent (existence checks), one per schema — order-independent by construction |
| Boundary erosion over time ("just one more join") | Adapter file is the single sanctioned edge + per-module test projects enforce reference isolation + README documents the rule |
| Output-cache/auth-policy drift while endpoints move | Route/auth/cache attributes copied verbatim; integration matrices assert identical status codes before/after |

## 13. Explicitly Deferred (extraction path)

When a module actually needs independence: point its `Database:Modules:*:ConnectionString` at a separate database, replace the host adapter with an HTTP/gRPC client or event consumer behind the same port. Nothing above pre-builds that — but nothing above blocks it either. That asymmetry is the whole point of the exercise.
