# Project Structure Audit & Restructure — Design

Date: 2026-08-24
Status: Approved (Approach A — normalized modular monolith)

## Context

The solution is a disciplined modular monolith targeting .NET 10: three feature modules
(Catalog, Identity, Negotiations), a shared `SharedKernel` kernel, and a single host.
Dependency rules are already strict (modules reference only SharedKernel; the host is the
composition root; one sanctioned cross-module edge via dependency inversion). This restructure
tightens ownership boundaries, normalizes layout, removes duplication and cruft. It changes no
API routes, database schemas/migrations, or feature behavior.

Audit findings addressed:

- `AppHost` name falsely implies .NET Aspire (it is a plain ASP.NET Core host).
- Leaky shared kernel: `SharedKernel.ProductQuery` is Catalog domain logic.
  `UserRoles` was re-examined and stays: it is cross-cutting authorization vocabulary used by
  the host policies plus Catalog, Identity, and Negotiations endpoint gates — same category as
  `Policies`/`ErrorCodes`.
- Asymmetric module internals: only Negotiations has `Ports/`; only Identity has `Public/` and
  `Auth/`; Negotiations' `Features/` is flat while Catalog nests per entity.
- Ownership is convention-only: module implementation types are `public`, so nothing prevents
  cross-assembly reach-ins at compile time.
- Duplicated plumbing: three near-identical seeding hosted services and design-time DbContext
  factories.
- Cruft: ghost `tests/PriceNegotiationApp.UnitTests/` directory (no csproj, untracked), unused
  `NSubstitute` CPM entry, dead transitive-overrides block in `Directory.Packages.props`.

## 1. Solution & project graph

```
PriceNegotiationApp.slnx
├── src/
│   ├── PriceNegotiationApp.Api/            ← renamed from AppHost
│   ├── PriceNegotiationApp.SharedKernel/ ← slimmed generic-only kernel
│   ├── PriceNegotiationApp.Modules.Catalog/
│   ├── PriceNegotiationApp.Modules.Identity/
│   └── PriceNegotiationApp.Modules.Negotiations/
└── tests/
    ├── PriceNegotiationApp.IntegrationTests/
    └── PriceNegotiationApp.Modules.{Catalog|Identity|Negotiations}.Tests/
```

- Dependency rule unchanged: modules reference **only** SharedKernel; Api references all
  modules as composition root; tests reference their subject (IntegrationTests → Api).
- The single inter-module edge remains `Ports/IProductPriceProvider` implemented by
  `Api/Composition/CatalogToNegotiations`.
- No projects added or merged; one rename (`AppHost` → `Api`) with full namespace updates.

## 2. Normalized module layout

Every module uses the identical structure:

```
Modules.X/
├── XModule.cs              ← public registration entry point
├── XEndpoints.cs           ← public endpoint mapping
├── Domain/                 ← entities, value objects, domain policies (internal)
├── Features/<Entity>/      ← one folder per feature group: handlers + models together
├── Persistence/            ← DbContext, configurations, migrations, design-time factory (internal)
├── Ports/                  ← interfaces this module needs from outside (public — the
│                             host must be able to implement them for DI wiring)
├── Public/                 ← contracts other assemblies may consume — ONLY public surface
└── Seeding/                ← module seeder built on shared base
```

Concrete moves:

- Negotiations: flat `Features/*` files move under `Features/Negotiations/`;
  `Features/NegotiationAccess.cs` and `Features/NegotiationModels.cs` follow.
- Identity: `Auth/JwtManager.cs`, `Auth/JwtOptions.cs`, `Auth/JwtOptionsValidator.cs` fold into
  `Features/Auth/`; `Features/Auth/` content stays there.
- Catalog gains `Public/` only if it has a cross-module contract today; if it exposes none, the
  folder is omitted rather than created empty.
- `UserRoles` stays in SharedKernel (see audit notes above).
- `ProductQuery` moves from SharedKernel to `Modules.Catalog/Features/Products/ProductQuery.cs`.

Rule: **`Public/` plus the two root files are the ownership boundary** — everything else in a
module is an implementation detail. Folders exist only when they have content; no empty folders
are created for symmetry's sake.

## 3. Slim shared kernel

`SharedKernel` keeps only assembly-agnostic primitives:

- `CallerContext` + extensions, `DbConnections`, `EndpointConventionExtensions`, `ErrorCodes`,
  `Exceptions`, `PagedResult`, `PageQuery`, `Policies`, `UserRoles`
- New shared plumbing bases: `ModuleSeedingHostedServiceBase`,
  `DesignTimeDbContextFactoryBase`

Nothing Catalog-, Identity-, or Negotiations-specific remains in the kernel.

## 4. Ownership enforcement (compile-time)

- All `Domain/`, `Features/`, `Persistence/`, `Seeding/` types become `internal`. Types in
  `Ports/` stay `public`: they are the module's required-services contract, which the host must
  implement and register against.
- Only `Public/` contents and `XModule.cs` / `XEndpoints.cs` remain `public`.
- The composition root (Api) is granted `InternalsVisibleTo` by each module: it legitimately
  reaches into module internals for the sanctioned adapter (`CatalogToNegotiations`) and the
  central exception mapper. This privilege applies only to the host — never to other modules.
- Test projects get access via `InternalsVisibleTo` in each module's csproj.
- Module-to-module access stays impossible: no module references another and internals are not
  visible to them; violations become build errors under the existing warnings-as-errors policy.

## 5. De-duplication

- Seeding: one generic base hosted service in SharedKernel; each module supplies DbContext,
  seed logic, and options binding only.
- Design-time factories: shared abstract base in SharedKernel; each module's factory reduces
  to a few lines naming its DbContext and connection string.
- The duplicated `Price` value object in Catalog vs Negotiations domains intentionally remains —
  bounded-context hygiene, not an accident.

## 6. Cruft & hygiene

- Delete ghost `tests/PriceNegotiationApp.UnitTests/`.
- Remove unused `NSubstitute` from `Directory.Packages.props`; remove the empty
  transitive-overrides block.
- Update `PriceNegotiationApp.http` host/port to match launchSettings.
- Update Dockerfile/docker-compose for the `Api` rename; refresh README architecture section.

## 7. Verification

1. Full solution build with warnings-as-errors and enforced code style (existing gates).
2. All test suites green: per-module unit tests + IntegrationTests (Testcontainers PostgreSQL)
   covering auth flow, products CRUD, negotiation lifecycle end-to-end.
3. Boundary audit greps: no module namespace referenced outside itself except through its
   `Public/` surface; host touches only public module entry points.
4. No new package dependencies introduced.

## Out of scope

- API route/response contract changes
- Database schema or migration changes
- Feature behavior changes
- Aspire adoption or service decomposition
