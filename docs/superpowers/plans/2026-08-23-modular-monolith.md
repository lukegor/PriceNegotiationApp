# Modular Monolith Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restructure PriceNegotiationApp into a modular monolith with one DbContext and one Postgres schema per bounded context (Identity, Catalog, Negotiations), compiler-enforced module boundaries, and zero behavioral drift on the HTTP contract (except one pinned change: product deletion is no longer blocked by negotiation history).

**Architecture:** Three vertical module projects own their domain, features, endpoints, and persistence. A thin `AppHost` composes them and wires the single cross-module seam (`IProductPriceProvider`) as a consumer-owned port / host-side adapter. `BuildingBlocks` holds ~12 stable shared types. The data-layer cutover happens *before* code moves, so EF-tooling risk never shares a phase with structural-move risk.

**Tech Stack:** .NET 10 / C# latest, ASP.NET Core minimal APIs, EF Core 10 + Npgsql (PostgreSQL 17), Vogen, Serilog, OpenTelemetry, xUnit v3 + Testcontainers.

**Spec:** `docs/superpowers/specs/2026-08-23-modular-monolith-design.md`

## Global Constraints

- Target framework `net10.0`; `TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true` come from `Directory.Build.props` — never weaken them.
- Central Package Management: package `Version`s appear only in `Directory.Packages.props`. New projects reference packages without versions; every needed version already exists.
- PostgreSQL: every context uses `.UseSnakeCaseNamingConvention()`; default schemas: `identity`, `catalog`, `negotiations`.
- Distinct migration-history tables, kept in the default `public` schema (avoids schema-existence ordering problems on virgin databases): `__EFMigrationsHistory_Identity`, `__EFMigrationsHistory_Catalog`, `__EFMigrationsHistory_Negotiations`.
- **No cross-schema foreign keys.** Logical references are plain Guid columns.
- HTTP routes, status codes (400/401/403/404/409/422/499), ProblemDetails `code` values, and JSON payload shapes are **frozen** — the existing integration suite is the regression gate.
- One pinned semantic change (spec §6): deleting a product succeeds even when negotiations reference it; those negotiations survive on their price snapshots.
- Integration tests need a running Docker daemon (Testcontainers). Check with `docker info` first.
- EF tooling: if `dotnet ef --version` fails, run `dotnet tool install --global dotnet-ef`.
- All commands run from repo root. Conventional commits, one commit per task unless a step says otherwise.

## Namespace Migration Map (apply mechanically wherever a file moves)

| Old | New |
|---|---|
| `PriceNegotiationApp.Application.Common` (CallerContext, PageQuery, PagedResult, ProductQuery) | `PriceNegotiationApp.BuildingBlocks` |
| `PriceNegotiationApp.Application.Common.ErrorCodes` (generic members only) | `PriceNegotiationApp.BuildingBlocks` (`ErrorCodes`) |
| `PriceNegotiationApp.Application.Common.UserRoles` | `PriceNegotiationApp.Modules.Identity.Public` (`UserRoles`) |
| `PriceNegotiationApp.Application.Exceptions.*` | `PriceNegotiationApp.BuildingBlocks` |
| `PriceNegotiationApp.Domain.Exceptions.DomainException` | `PriceNegotiationApp.BuildingBlocks` |
| `PriceNegotiationApp.Domain.Models` (Negotiation, Customer, enums) | `PriceNegotiationApp.Modules.Negotiations.Domain` |
| `PriceNegotiationApp.Domain.Policy.*` | `PriceNegotiationApp.Modules.Negotiations.Domain` |
| `PriceNegotiationApp.Domain.ValueObjects.Ids.NegotiationId/CustomerId` | `PriceNegotiationApp.Modules.Negotiations.Domain` |
| `PriceNegotiationApp.Infrastructure.Auth.*` | `PriceNegotiationApp.Modules.Identity.Auth` |
| `PriceNegotiationApp.Infrastructure.Identity.ApplicationUser` | `PriceNegotiationApp.Modules.Identity.Persistence` |
| `PriceNegotiationApp.Infrastructure.Seeding.SeedingOptions` | `PriceNegotiationApp.Modules.Identity.Seeding` |

Deleted outright (no migration path): `Entity`, `IBusinessRule`, `Domain/Models/Rules/*`, `IUnitOfWork`+`UnitOfWork`, `IProductRepository`+impl, `INegotiationRepository`+impl, `ICustomerRepository`+impl, `IUserAccountStore`+`IdentityAccountStore`, `RegistrationOutcome`, `SignInResultKind`, `IAuthService`/`IProductService`/`INegotiationService`, projects `Application`, `Domain`, `Infrastructure`, `Api`.

## File Structure (end state)

```
src/
  PriceNegotiationApp.AppHost/
    Program.cs
    GlobalExceptionHandler.cs
    Extensions/{PipelineExtensions,WebApplicationBuilderExtensions,JwtSettings,RateLimitingOptions}.cs
    Composition/{MigrationHostedService,CatalogToNegotiations}.cs
    appsettings.json, Properties/launchSettings.json
  PriceNegotiationApp.BuildingBlocks/
    CallerContext.cs PageQuery.cs PagedResult.cs ProductQuery.cs
    ErrorCodes.cs Policies.cs Exceptions.cs EndpointConventionExtensions.cs
    DbConnections.cs CallerContextExtensions.cs
  PriceNegotiationApp.Modules.Catalog/
    CatalogModule.cs
    Domain/Product.cs Domain/Price.cs
    Persistence/{CatalogDbContext,ProductConfiguration,DesignTimeDbContextFactory}.cs
    Persistence/Migrations/*
    Seeding/{CatalogSeedingHostedService,CatalogSeedingOptions}.cs
    Features/Products/{List,Get,Create,Update,Delete}.cs
    Features/Products/ProductModels.cs        (requests + ProductResponse)
  PriceNegotiationApp.Modules.Negotiations/
    NegotiationsModule.cs
    Ports/IProductPriceProvider.cs            (+ ProductSnapshot record)
    Domain/{Negotiation,Customer,NegotiationStatus,NegotiationOutcome,
            NegotiationId,CustomerId,Price,INegotiationPolicy,
            DefaultNegotiationPolicy,NegotiationExceptions}.cs
    Persistence/{NegotiationsDbContext,DesignTimeDbContextFactory}.cs
    Persistence/Configurations/{CustomerConfiguration,NegotiationConfiguration}.cs
    Persistence/Migrations/*
    Features/Negotiations/{Create,ListMine,List,Get,CounterPropose,
                           Accept,Decline,Withdraw,NegotiationAccess}.cs
    Features/Negotiations/NegotiationModels.cs   (requests, responses, NegotiationErrorCodes)
  PriceNegotiationApp.Modules.Identity/
    IdentityModule.cs
    Public/UserRoles.cs Public/IdentityErrorCodes.cs
    Auth/{JwtManager,JwtOptions,JwtOptionsValidator}.cs
    Persistence/{IdentityModuleDbContext,ApplicationUser,DesignTimeDbContextFactory}.cs
    Persistence/Migrations/*
    Seeding/{IdentitySeedingHostedService,IdentitySeedingOptions}.cs
    Features/Auth/{Register,Login,Me,AuthModels}.cs
docs/sql/legacy-data-migration.sql
tests/
  PriceNegotiationApp.Modules.Identity.Tests/
  PriceNegotiationApp.Modules.Catalog.Tests/
  PriceNegotiationApp.Modules.Negotiations.Tests/
  PriceNegotiationApp.IntegrationTests/      (harness unchanged; +2 cases)
```

---

### Task 0: Repository hygiene

**Files:**
- Delete (untracked junk): root-level `PriceNegotiationApp.{Api,Application,Contracts,Domain,Infrastructure,Presentation,SharedKernel}/` (contain only `bin/`+`obj/`), `src/logs/`
- Modify: `.gitignore`

**Interfaces:**
- Consumes: nothing
- Produces: clean working tree so later `git add -A` steps are unambiguous

- [ ] **Step 1: Delete stale artifacts**

```powershell
Remove-Item -Recurse -Force `
  PriceNegotiationApp.Api, PriceNegotiationApp.Application, PriceNegotiationApp.Contracts, `
  PriceNegotiationApp.Domain, PriceNegotiationApp.Infrastructure, PriceNegotiationApp.Presentation, `
  PriceNegotiationApp.SharedKernel, src/logs -ErrorAction SilentlyContinue
```

- [ ] **Step 2: Ignore local env + logs**

Append to `.gitignore`:

```gitignore
.env
logs/
```

Untrack the placeholder copy but keep `.env.example`: `git rm --cached .env`

- [ ] **Step 3: Validate**

```powershell
dotnet build PriceNegotiationApp.slnx
```
Must succeed — the slnx points at `src/` only, nothing referenced the deleted folders.

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "chore: remove stale build artifacts, ignore .env and logs"
```

---

### Task 1: Split AppDbContext into three additive contexts

Legacy `AppDbContext` keeps serving everything. The three new contexts are registered, migrated by tooling, and health-checked, but unused by repositories yet. Purely additive → zero regression risk.

**Files:**
- Create: `src/PriceNegotiationApp.Infrastructure/Persistence/IdentityModuleDbContext.cs`
- Create: `src/PriceNegotiationApp.Infrastructure/Persistence/CatalogDbContext.cs`
- Create: `src/PriceNegotiationApp.Infrastructure/Persistence/NegotiationsDbContext.cs`
- Create: `src/PriceNegotiationApp.Infrastructure/Data/DesignTimeFactories.cs` (replaces `Data/DesignTimeDbContextFactory.cs`)
- Modify: `src/PriceNegotiationApp.Infrastructure/DependencyInjection.cs`
- Modify: `src/PriceNegotiationApp.Api/Extensions/WebApplicationBuilderExtensions.cs` (health checks)

**Interfaces:**
- Produces (exact types later tasks rely on):
  - `CatalogDbContext(DbContextOptions<CatalogDbContext>)` with `DbSet<Product> Products`
  - `NegotiationsDbContext(DbContextOptions<NegotiationsDbContext>)` with `DbSet<Customer> Customers`, `DbSet<Negotiation> Negotiations`
  - `IdentityModuleDbContext(DbContextOptions<IdentityModuleDbContext>) : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`
  - Default schemas set inside each context's `OnModelCreating` via `HasDefaultSchema` (single source of truth); history tables per Global Constraints.

- [ ] **Step 1: Tooling check**

```powershell
dotnet ef --version   # if missing: dotnet tool install --global dotnet-ef
```

- [ ] **Step 2: Create the three contexts**

`IdentityModuleDbContext.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Infrastructure.Identity;

namespace PriceNegotiationApp.Infrastructure.Persistence;

public sealed class IdentityModuleDbContext(DbContextOptions<IdentityModuleDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("identity");
        // Pin snake_case names so Identity stores never depend on naming conventions.
        builder.Entity<ApplicationUser>().ToTable("users");
        builder.Entity<IdentityRole<Guid>>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
    }
}
```

`CatalogDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Domain.Models;
using PriceNegotiationApp.Infrastructure.Persistence.DbEntityConfigurations;

namespace PriceNegotiationApp.Infrastructure.Persistence;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("catalog");
        // Explicit registration: configurations are owned per context, never assembly-scanned.
        builder.ApplyConfiguration(new ProductConfiguration());
    }
}
```

`NegotiationsDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Domain.Models;
using PriceNegotiationApp.Infrastructure.Persistence.DbEntityConfigurations;

namespace PriceNegotiationApp.Infrastructure.Persistence;

public sealed class NegotiationsDbContext(DbContextOptions<NegotiationsDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Negotiation> Negotiations => Set<Negotiation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("negotiations");
        builder.ApplyConfiguration(new CustomerConfiguration());
        builder.ApplyConfiguration(new NegotiationConfiguration());
    }
}
```

- [ ] **Step 3: Register contexts**

In `DependencyInjection.AddInfrastructure`, keep the existing `AddDbContext<AppDbContext>` block and add below it:

```csharp
var connectionString = configuration["Database:ConnectionString"];
services.AddDbContext<IdentityModuleDbContext>(options => options
    .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Identity"))
    .UseSnakeCaseNamingConvention());
services.AddDbContext<CatalogDbContext>(options => options
    .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Catalog"))
    .UseSnakeCaseNamingConvention());
services.AddDbContext<NegotiationsDbContext>(options => options
    .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Negotiations"))
    .UseSnakeCaseNamingConvention());
```

- [ ] **Step 4: Health checks report all four (transitional)**

In `WebApplicationBuilderExtensions.AddApiServices` replace the single DB check line with:

```csharp
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddDbContextCheck<AppDbContext>("database-legacy", tags: ["ready"])
    .AddDbContextCheck<IdentityModuleDbContext>("database-identity", tags: ["ready"])
    .AddDbContextCheck<CatalogDbContext>("database-catalog", tags: ["ready"])
    .AddDbContextCheck<NegotiationsDbContext>("database-negotiations", tags: ["ready"]);
```

(The legacy check disappears in Task 2.)

- [ ] **Step 5: Replace the design-time factory**

Delete `Data/DesignTimeDbContextFactory.cs`; create `Data/DesignTimeFactories.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PriceNegotiationApp.Infrastructure.Data;

public sealed class IdentityDesignTimeFactory : IDesignTimeDbContextFactory<IdentityModuleDbContext>
{
    public IdentityModuleDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<IdentityModuleDbContext>()
            .UseNpgsql(DesignTime.ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Identity"))
            .UseSnakeCaseNamingConvention()
            .Options);
}

public sealed class CatalogDesignTimeFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(DesignTime.ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Catalog"))
            .UseSnakeCaseNamingConvention()
            .Options);
}

public sealed class NegotiationsDesignTimeFactory : IDesignTimeDbContextFactory<NegotiationsDbContext>
{
    public NegotiationsDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<NegotiationsDbContext>()
            .UseNpgsql(DesignTime.ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Negotiations"))
            .UseSnakeCaseNamingConvention()
            .Options);
}

internal static class DesignTime
{
    internal const string ConnectionString =
        "Host=localhost;Port=5432;Database=pricenego_design;Username=postgres;Password=postgres";
}
```

Note: factories intentionally omit connection-string env overrides — schema diffing does not need a live database.

- [ ] **Step 6: Generate initial migrations for the three new contexts**

```powershell
dotnet ef migrations add Initial --context IdentityModuleDbContext -p src/PriceNegotiationApp.Infrastructure -o Persistence/Migrations/Identity
dotnet ef migrations add Initial --context CatalogDbContext        -p src/PriceNegotiationApp.Infrastructure -o Persistence/Migrations/Catalog
dotnet ef migrations add Initial --context NegotiationsDbContext   -p src/PriceNegotiationApp.Infrastructure -o Persistence/Migrations/Negotiations
```

Verify in the generated files: identity tables carry schema `identity`; products → `catalog.products`; negotiations/customers → `negotiations.*`; partial index filter `status = 1` present. The negotiations migration WILL contain an FK to catalog.products at this point (current configuration still declares it) — expected; removed and regenerated in Task 2.

- [ ] **Step 7: Validate**

```powershell
dotnet build PriceNegotiationApp.slnx
dotnet test tests/PriceNegotiationApp.UnitTests
dotnet test tests/PriceNegotiationApp.IntegrationTests
```

All green: new contexts registered, migrated on startup (nothing migrates them yet except tooling verification — startup migration wiring lands in Task 2), health-checked.

- [ ] **Step 8: Commit**

```bash
git add -A && git commit -m "feat(persistence): add identity/catalog/negotiations contexts with schemas"
```

---

### Task 2: Cutover — repositories and hosting move to the new contexts; AppDbContext retired

The contained-risk gate: all reads/writes flow through the three contexts, hosting splits into a migrator + two seeders, the legacy context and its migrations are deleted, the cross-schema FK disappears (pinned semantic change), and the new integration tests land.

**Files:**
- Modify: `src/PriceNegotiationApp.Infrastructure/Persistence/Repositories/{ProductRepository,NegotiationRepository,CustomerRepository,UnitOfWork}.cs`
- Modify: `src/PriceNegotiationApp.Infrastructure/Persistence/DbEntityConfigurations/NegotiationConfiguration.cs`
- Create: `src/PriceNegotiationApp.Infrastructure/Hosting/MigrationHostedService.cs`
- Create: `src/PriceNegotiationApp.Infrastructure/Seeding/IdentitySeedingHostedService.cs`
- Create: `src/PriceNegotiationApp.Infrastructure/Seeding/CatalogSeedingHostedService.cs`
- Delete: `src/PriceNegotiationApp.Infrastructure/Seeding/SeedingHostedService.cs`, `src/PriceNegotiationApp.Infrastructure/Data/AppDbContext.cs` (+ `Data/Migrations/*` single-context set)
- Modify: `src/PriceNegotiationApp.Infrastructure/DependencyInjection.cs`
- Modify: `src/PriceNegotiationApp.Api/Extensions/WebApplicationBuilderExtensions.cs` (health checks)
- Create: `docs/sql/legacy-data-migration.sql`
- Test: `tests/PriceNegotiationApp.IntegrationTests/NegotiationsShould.cs` (add 2 cases)

**Interfaces:**
- Consumes: contexts from Task 1.
- Produces:
  - `MigrationHostedService : IHostedService` — applies Identity → Catalog → Negotiations migrations in order, fail-fast.
  - `IdentitySeedingHostedService : IHostedService` — roles + admin/staff users from `SeedingOptions`.
  - `CatalogSeedingHostedService : IHostedService` — sample products when `SeedSampleProducts` is true.
  - `UnitOfWork(IEnumerable<DbContext>)` transitional implementation of `IUnitOfWork` (dies with legacy projects in Task 7).

- [ ] **Step 1: Rewire repositories to their contexts**

`ProductRepository`: change constructor to `ProductRepository(CatalogDbContext db)`; body unchanged otherwise (`db.Products...`). Same using set minus nothing.

`NegotiationRepository`: constructor becomes `NegotiationRepository(NegotiationsDbContext db, ICustomerRepository customers)`; body unchanged.

`CustomerRepository`: constructor becomes `CustomerRepository(NegotiationsDbContext db, IUnitOfWork uow)`; body unchanged.

- [ ] **Step 2: Drop the cross-schema FK**

In `NegotiationConfiguration`, delete this line:

```csharp
builder.HasOne<Product>().WithMany().HasForeignKey(n => n.ProductId).OnDelete(DeleteBehavior.Restrict);
```

Replace it with a comment documenting the decision:

```csharp
// No FK to catalog.products by design (separate schemas/modules). Product existence is
// validated at negotiation creation; negotiations survive product deletion on snapshots.
```

- [ ] **Step 3: Transitional UnitOfWork saves all dirty contexts**

Rewrite `UnitOfWork.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Application.Abstractions;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Application.Exceptions;

namespace PriceNegotiationApp.Infrastructure.Persistence.Repositories;

/// <summary>Transitional: saves every registered context that has pending changes.
/// Removed together with the legacy projects once modules own their save points.</summary>
public sealed class UnitOfWork(IEnumerable<DbContext> contexts) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken ct)
    {
        var saved = 0;
        foreach (var db in contexts)
        {
            if (!db.ChangeTracker.HasChanges())
            {
                continue;
            }

            try
            {
                saved += await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConflictException(ErrorCodes.ConcurrencyConflict,
                    "The resource was modified concurrently. Reload and retry.");
            }
        }

        return saved;
    }
}
```

Register it in `DependencyInjection` (replace the existing scoped registration):

```csharp
services.AddScoped<IUnitOfWork, UnitOfWork>();
services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());
services.AddScoped<DbContext>(sp => sp.GetRequiredService<CatalogDbContext>());
services.AddScoped<DbContext>(sp => sp.GetRequiredService<NegotiationsDbContext>());
```

(The `IEnumerable<DbContext>` registration picks up all three `AddScoped<DbContext>` factories. `AppDbContext` dies in this task — see Step 6 — so drop that first `AddScoped<DbContext>` line there.)

- [ ] **Step 4: Migration + seeding hosted services**

Create `src/PriceNegotiationApp.Infrastructure/Hosting/MigrationHostedService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PriceNegotiationApp.Infrastructure.Persistence;

namespace PriceNegotiationApp.Infrastructure.Hosting;

public sealed class MigrationHostedService(IServiceScopeFactory scopeFactory, ILogger<MigrationHostedService> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await MigrateAsync<IdentityModuleDbContext>(scope, cancellationToken);
        await MigrateAsync<CatalogDbContext>(scope, cancellationToken);
        await MigrateAsync<NegotiationsDbContext>(scope, cancellationToken);
        logger.LogInformation("Module databases migrated.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task MigrateAsync<T>(IServiceScope scope, CancellationToken ct) where T : DbContext
    {
        var db = scope.ServiceProvider.GetRequiredService<T>();
        await db.Database.MigrateAsync(ct);
    }
}
```

Create `Seeding/IdentitySeedingHostedService.cs` (logic carried verbatim from old `SeedingHostedService` minus migration and products):

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceNegotiationApp.Modules.Identity.Public;
using PriceNegotiationApp.Infrastructure.Identity;
using PriceNegotiationApp.Infrastructure.Seeding;

namespace PriceNegotiationApp.Infrastructure.Seeding;

public sealed class IdentitySeedingHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<SeedingOptions> options,
    ILogger<IdentitySeedingHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { UserRoles.Admin, UserRoles.Staff, UserRoles.Customer })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await EnsureUserAsync(userManager, options.Value.AdminEmail, options.Value.AdminPassword, UserRoles.Admin);
        await EnsureUserAsync(userManager, options.Value.StaffEmail, options.Value.StaffPassword, UserRoles.Staff);
        logger.LogInformation("Identity seed data ensured.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager, string email, string password, string role)
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
    }
}
```

Note: `PriceNegotiationApp.Modules.Identity.Public` does not exist yet — until Task 6, keep `using PriceNegotiationApp.Application.Common;` for `UserRoles` instead, and swap the using in Task 6.

Create `Seeding/CatalogSeedingHostedService.cs` (product block extracted verbatim):

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceNegotiationApp.Domain.Models;
using PriceNegotiationApp.Infrastructure.Persistence;
using PriceNegotiationApp.Infrastructure.Seeding;

namespace PriceNegotiationApp.Infrastructure.Seeding;

public sealed class CatalogSeedingHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<SeedingOptions> options,
    ILogger<CatalogSeedingHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.SeedSampleProducts)
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        if (!await db.Products.AnyAsync(cancellationToken))
        {
            db.Products.AddRange(
                Product.Create("Mechanical Keyboard", 249.00m),
                Product.Create("Wireless Mouse", 79.90m),
                Product.Create("USB-C Docking Station", 189.50m));
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Catalog seed data ensured.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

Update `DependencyInjection`: remove `services.AddHostedService<SeedingHostedService>();` and add, **in this exact order** (hosted services start sequentially in registration order):

```csharp
services.AddHostedService<MigrationHostedService>();
services.AddHostedService<IdentitySeedingHostedService>();
services.AddHostedService<CatalogSeedingHostedService>();
```

Also delete from DI the `AddDbContext<AppDbContext>` block and the `AddScoped<DbContext>(sp => ...AppDbContext...)` line from Step 3.

Health checks: replace the four checks with three (drop `database-legacy`).

- [ ] **Step 5: Regenerate the Negotiations migration (FK removal)**

```powershell
dotnet ef migrations add DropProductForeignKey --context NegotiationsDbContext -p src/PriceNegotiationApp.Infrastructure -o Persistence/Migrations/Negotiations
```

Verify the new migration contains exactly one `DropForeignKey` operation and no others.

- [ ] **Step 6: Delete the legacy context and its migration set**

```powershell
Remove-Item -Recurse -Force src/PriceNegotiationApp.Infrastructure/Persistence/AppDbContext.cs, `
  src/PriceNegotiationApp.Infrastructure/Data/Migrations
git add -A
```

If the build now fails anywhere referencing `AppDbContext` (should be none after Steps 1–4), fix those references to use the module contexts.

- [ ] **Step 7: Legacy-data SQL script**

Create `docs/sql/legacy-data-migration.sql` — run manually against a pre-refactor database before starting the new app version:

```sql
-- One-time migration: pre-modular schema (public.*) -> module schemas.
-- Run BEFORE starting the new application version against an existing database.
-- Identity columns are identical between layouts; only table locations change.

BEGIN;

-- Catalog
INSERT INTO catalog.products (id, name, price, "Version")
SELECT id, name, price, xmin FROM public.products
ON CONFLICT DO NOTHING;

-- Negotiations
INSERT INTO negotiations.customers (id, identity_user_id)
SELECT id, identity_user_id FROM public.customers
ON CONFLICT DO NOTHING;

INSERT INTO negotiations.negotiations
    (id, product_id, customer_id, base_price, current_offer, status,
     proposals_used, created_at_utc, last_proposal_at_utc, decided_at_utc, "Version")
SELECT id, product_id, customer_id, base_price, current_offer, status,
       proposals_used, created_at_utc, last_proposal_at_utc, decided_at_utc, xmin
FROM public.negotiations
ON CONFLICT DO NOTHING;

COMMIT;
```

Column-name caveat: verify actual snake_case column names against the old `20260823155421_Initial.cs` migration before running (adjust `"Version"` — xmin is selected as the row version source; EF maps `uint Version` to xmin so the physical column IS xmin and must not be inserted directly — if the old table has no separate version column, drop the `xmin AS "Version"` projection and the `"Version"` target column entirely).

Create `docs/sql/cleanup-legacy-tables.sql` (run after verifying cutover on a persistent environment):

```sql
DROP TABLE IF EXISTS public.negotiations CASCADE;
DROP TABLE IF EXISTS public.customers CASCADE;
DROP TABLE IF EXISTS public.products CASCADE;
DROP TABLE IF EXISTS public.__efmigrations_history CASCADE;
```

- [ ] **Step 8: Integration tests pinning new behavior**

Append inside `tests/PriceNegotiationApp.IntegrationTests/NegotiationsShould.cs` class:

```csharp
[Fact]
public async Task SurviveWhenReferencedProductIsDeleted()
{
    var staff = await fixture.LoginAsStaffAsync();
    var create = await staff.Client.PostAsJsonAsync("/api/v1/products",
        new { name = "Doomed Product", price = 100m });
    var product = await create.Content.ReadFromJsonAsync<ProductResponse>();
    var customer = await fixture.CreateUserAsync();

    var negotiation = await customer.Client.PostAsJsonAsync("/api/v1/negotiations",
        new { productId = product!.Id, proposedPrice = 90m });
    negotiation.EnsureSuccessStatusCode();

    var delete = await staff.Client.DeleteAsync($"/api/v1/products/{product.Id}");
    delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

    var mine = await customer.Client.GetAsync("/api/v1/negotiations/mine?page=1&pageSize=10");
    mine.EnsureSuccessStatusCode();
    var page = await mine.Content.ReadFromJsonAsync<PagedNegotiations>();
    page!.TotalCount.ShouldBe(1);
}

[Fact]
public async Task ReadyEndpointReportsAllModuleSchemas()
{
    var response = await fixture.Anonymous.GetAsync("/health/ready");
    response.EnsureSuccessStatusCode();
}
```

Add support record (new file `Support/PagedNegotiations.cs`):

```csharp
namespace PriceNegotiationApp.IntegrationTests.Support;

public sealed record PagedNegotiations(
    IReadOnlyList<NegotiationDto> Items, int Page, int PageSize, long TotalCount);

public sealed record NegotiationDto(Guid Id, Guid ProductId, decimal BasePrice, decimal CurrentOffer);
```

Check `ProductsShould.cs` for an existing test asserting that deleting a referenced product FAILS — if present, invert its expectation to match the pinned change (204 now succeeds); if none exists, the new case above covers it.

- [ ] **Step 9: Validate**

```powershell
dotnet build PriceNegotiationApp.slnx
dotnet test tests/PriceNegotiationApp.UnitTests
dotnet test tests/PriceNegotiationApp.IntegrationTests
```

Full suite green including the two new cases. This proves end-to-end: three schemas migrate, seeders populate identity+catalog, all endpoints serve off the split contexts.

- [ ] **Step 10: Commit**

```bash
git add -A && git commit -m "feat(persistence): cut over to per-module contexts, retire AppDbContext"
```

---

### Task 3: Extract BuildingBlocks

New zero-dependency shared project; all existing projects re-point to it. Behavior unchanged.

**Files:**
- Create: `src/PriceNegotiationApp.BuildingBlocks/PriceNegotiationApp.BuildingBlocks.csproj` + the 8 source files below
- Modify: every project's `.csproj` (add `ProjectReference` to BuildingBlocks where it consumes BB types) and every file whose `using` changes per the Namespace Migration Map
- Delete after sweep: `src/PriceNegotiationApp.Application/Common/*`, `src/PriceNegotiationApp.Application/Exceptions/*`, `src/PriceNegotiationApp.Domain/Abstractions/{Entity,IBusinessRule}.cs`, `src/PriceNegotiationApp.Domain/Exceptions/DomainException.cs`

**Interfaces:**
- Produces (namespace `PriceNegotiationApp.BuildingBlocks` for ALL of these):
  - `CallerContext(Guid UserId, string Email, IReadOnlySet<string> Roles)` + `.Anonymous`, `.IsAuthenticated`, `.IsInRole(string)`
  - `PageQuery(int Page, int PageSize)` + `.SafePage`, `.SafePageSize`, `.Skip`
  - `PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount)`
  - `ProductQuery(string? Search, decimal? MinPrice, decimal? MaxPrice, string? SortBy, bool SortDesc, int Page, int PageSize)`
  - `ErrorCodes`: `Forbidden="forbidden"`, `ConcurrencyConflict="conflict"`, `ValidationFailed="validation_failed"`, `DomainRuleViolated="domain_rule_violated"`, `InternalError="internal_error"`
  - `Policies`: `AuthRateLimitPolicy="auth"`, `ShortCachePolicy="short"`
  - Exceptions: `NotFoundException(string entityName, object key)` with `.Code => $"{entity}_not_found"`; `ConflictException(string code, string message)`; `InvalidRequestException(string code, string message)`; `UnauthorizedException(string code, string message)`; `ForbiddenAccessException()` — bodies copied verbatim from current `Application/Exceptions/*`
  - `DomainException(string message)` (verbatim from Domain)
  - `RequireRoles<TBuilder>(this TBuilder, params string[] roles)` (verbatim from Api `EndpointConventionExtensions`)

- [ ] **Step 1: Project file**

```xml
<Project Sdk="Microsoft.NET.Sdk">
</Project>
```

(No packages. ImplicitUsings/nullable come from Directory.Build.props.)

- [ ] **Step 2: Source files**

Move with namespace change only (bodies verbatim): `CallerContext.cs`, `PageQuery.cs`, `PagedResult.cs`, `ProductQuery.cs`, and the five exception files listed above, plus `DomainException.cs`. Then create:

`ErrorCodes.cs`:

```csharp
namespace PriceNegotiationApp.BuildingBlocks;

public static class ErrorCodes
{
    public const string Forbidden = "forbidden";
    public const string ConcurrencyConflict = "conflict";
    public const string ValidationFailed = "validation_failed";
    public const string DomainRuleViolated = "domain_rule_violated";
    public const string InternalError = "internal_error";
}
```

(The feature-specific members — `ProductNotFound` … `RegistrationInvalid` — are NOT carried here. They move into modules in Tasks 4–6 as `NegotiationErrorCodes` / `IdentityErrorCodes`. Until those tasks, keep a temporary copy of the removed members in each consuming file as `const string` locals if compilation requires.)

`Policies.cs`:

```csharp
namespace PriceNegotiationApp.BuildingBlocks;

/// <summary>Shared policy names so host registrations and module endpoint annotations agree.</summary>
public static class Policies
{
    public const string AuthRateLimitPolicy = "auth";

    public const string ShortCachePolicy = "short";
}
```

`EndpointConventionExtensions.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;

namespace PriceNegotiationApp.BuildingBlocks;

public static class EndpointConventionExtensions
{
    public static TBuilder RequireRoles<TBuilder>(this TBuilder builder, params string[] roles)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(new AuthorizeAttribute { Roles = string.Join(',', roles) });
}
```

Because of the ASP.NET types, the csproj needs one line inside `<Project>`:

```xml
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
```

- [ ] **Step 3: Re-point consumers**

1. Add `<ProjectReference Include="..\PriceNegotiationApp.BuildingBlocks\PriceNegotiationApp.BuildingBlocks.csproj" />` to Application, Infrastructure, Api csprojs.
2. Global find/replace across `src/`:
   - `using PriceNegotiationApp.Application.Common;` → `using PriceNegotiationApp.BuildingBlocks;`
   - `using PriceNegotiationApp.Application.Exceptions;` → `using PriceNegotiationApp.BuildingBlocks;`
   - `using PriceNegotiationApp.Domain.Exceptions;` → add `using PriceNegotiationApp.BuildingBlocks;` (keep the old using only in files still referencing Closed/Proposal exceptions — NegotiationsModule handler and GlobalExceptionHandler).
3. In `WebApplicationBuilderExtensions` replace the two const declarations (`AuthRateLimitPolicy`, `ShortCachePolicy`) and their usages with `BuildingBlocks.Policies.*` (delete the consts; update `AuthModule`, `ProductsModule` references).
4. Delete moved originals from Application/Domain.
5. Feature-specific error codes: `NegotiationsService` references `ErrorCodes.NegotiationAlreadyOpen` etc. Add a temporary internal static class in the Application project:

```csharp
namespace PriceNegotiationApp.Application.Common;

internal static class LegacyErrorCodes
{
    public const string NegotiationAlreadyOpen = "negotiation_already_open";
    public const string NoProposalsRemaining = "no_proposals_remaining";
    public const string ProposalExceedsLimit = "proposal_exceeds_limit";
    public const string NegotiationClosed = "negotiation_closed";
    public const string EmailAlreadyRegistered = "email_already_registered";
    public const string InvalidCredentials = "invalid_credentials";
    public const string AccountLocked = "account_locked";
    public const string RegistrationInvalid = "registration_invalid";
}
```

and switch the affected call sites to `LegacyErrorCodes.*`. These constants land in their real homes in Tasks 4–6, then this class dies.

- [ ] **Step 4: Validate**

```powershell
dotnet build PriceNegotiationApp.slnx
dotnet test tests/PriceNegotiationApp.UnitTests
dotnet test tests/PriceNegotiationApp.IntegrationTests
```

Green. Note: `GlobalExceptionHandler` keeps compiling because its remaining domain references (`ClosedNegotiationException`, `ProposalExceedsLimitException`) still live under the old Domain namespace until Task 4 moves them.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "refactor: extract BuildingBlocks shared kernel"
```

---

### Task 3a: BuildingBlocks additions needed by modules

Small follow-up so module tasks can consume two more shared pieces without re-opening Task 3's scope.

**Files:**
- Modify: `Directory.Packages.props` (one version entry), `src/PriceNegotiationApp.BuildingBlocks/PriceNegotiationApp.BuildingBlocks.csproj`
- Create: `src/PriceNegotiationApp.BuildingBlocks/DbConnections.cs`
- Move: `src/PriceNegotiationApp.Api/Extensions/ClaimsPrincipalExtensions.cs` → `src/PriceNegotiationApp.BuildingBlocks/CallerContextExtensions.cs`

**Interfaces:**
- Produces:
  - `DbConnections.Resolve(IConfiguration, string moduleName): string` — returns `Database:Modules:{name}:ConnectionString` when set, else `Database:ConnectionString`, else throws `InvalidOperationException`.
  - `CallerContextExtensions.ToCallerContext(this ClaimsPrincipal)` in namespace `PriceNegotiationApp.BuildingBlocks` (body verbatim).

- [ ] **Step 1: CPM entry + package reference**

In `Directory.Packages.props`, alongside the other 10.0.x entries add:

```xml
<PackageVersion Include="Microsoft.Extensions.Configuration.Abstractions" Version="10.0.8" />
```

In the BuildingBlocks csproj add:

```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
  </ItemGroup>
```

(`DependencyInjection.Abstractions` is already in CPM; it is included because module classes call `IServiceCollection` extensions defined there.)

- [ ] **Step 2: Create `DbConnections.cs`**

```csharp
using Microsoft.Extensions.Configuration;

namespace PriceNegotiationApp.BuildingBlocks;

public static class DbConnections
{
    private const string DefaultKey = "Database:ConnectionString";

    /// <summary>Per-module override wins; falls back to the shared connection string.</summary>
    public static string Resolve(IConfiguration configuration, string moduleName)
    {
        var moduleOverride = configuration[$"Database:Modules:{moduleName}:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(moduleOverride))
        {
            return moduleOverride;
        }

        return configuration[DefaultKey]
               ?? throw new InvalidOperationException(
                   $"{DefaultKey} is not configured (module '{moduleName}').");
    }
}
```

- [ ] **Step 3: Move caller-context mapping**

Move the file, rename to `CallerContextExtensions.cs`, class to `CallerContextExtensions`, namespace `PriceNegotiationApp.BuildingBlocks`. Delete the Api original and switch every consumer (`AuthModule`-replacement handlers, legacy modules) to `using PriceNegotiationApp.BuildingBlocks;`.

- [ ] **Step 4: Validate + commit**

```powershell
dotnet build PriceNegotiationApp.slnx && dotnet test tests/PriceNegotiationApp.UnitTests
git add -A && git commit -m "refactor(building-blocks): add DbConnections resolver and CallerContext mapping"
```


---

### Task 4: Carve out the Negotiations module

**The largest task:** new module project receives the Negotiation/Customer domain, its own DbContext + migrations, consumer-owned port, per-operation endpoints, and its unit-test project. Legacy negotiation service/repos/endpoints die here.

**Files:**
- Create: `src/PriceNegotiationApp.Modules.Negotiations/**` (all files below)
- Create: `tests/PriceNegotiationApp.Modules.NegotiationsTests/**` (project name without extra dot: `PriceNegotiationApp.Modules.Negotiations.Tests`)
- Modify: `src/PriceNegotiationApp.PriceNegotiationApp.slnx`, Api `GlobalExceptionHandler.cs` (usings), `Program.cs`-side registration (via `WebApplicationBuilderExtensions`), `PipelineExtensions.cs` (endpoint mapping)
- Delete: `Domain/Models/Negotiation.cs`, `Customer.cs`, `NegotiationOutcome.cs`, `NegotiationStatus.cs`, `Domain/ValueObjects/Ids/{NegotiationId,CustomerId}.cs`, `Domain/ValueObjects/Price.cs`, `Domain/Policy/*`, `Application/Features/Negotiations/*`, `Application/Abstractions/{INegotiationRepository,ICustomerRepository}.cs`, `Infrastructure/Persistence/Repositories/{NegotiationRepository,CustomerRepository}.cs`, `Api/Modules/NegotiationsModule.cs`, `tests/...UnitTests/Application/NegotiationServiceShould.cs`, Infrastructure `Persistence/NegotiationsDbContext.cs`, `Persistence/Migrations/Negotiations/`

**Interfaces:**
- Consumes: BuildingBlocks types (Task 3).
- Produces:
  - `NegotiationsModule.AddNegotiationsModule(this IServiceCollection, IConfiguration)` / `.MapNegotiationsEndpoints(this IEndpointRouteBuilder)`
  - `IProductPriceProvider { Task<ProductSnapshot?> GetAsync(Guid productId, CancellationToken ct); }`, `readonly record struct ProductSnapshot(Guid ProductId, decimal Price)`
  - `Negotiation.Start(CustomerId customerId, Guid productId, decimal basePriceSnapshot, decimal initialOffer, DateTimeOffset now, INegotiationPolicy policy)`
  - `NegotiationErrorCodes`: `NegotiationClosed="negotiation_closed"`, `ProposalExceedsLimit="proposal_exceeds_limit"`, `NegotiationAlreadyOpen="negotiation_already_open"`, `NoProposalsRemaining="no_proposals_remaining"` (public — AppHost handler reads the first two)

- [ ] **Step 1: Project file**

`src/PriceNegotiationApp.Modules.Negotiations/PriceNegotiationApp.Modules.Negotiations.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <!-- Vogen-generated IComparable implementations trigger MA0097 on the declaring partial. -->
    <NoWarn>$(NoWarn);MA0097</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\PriceNegotiationApp.BuildingBlocks\PriceNegotiationApp.BuildingBlocks.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="EFCore.NamingConventions" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
    <PackageReference Include="Vogen" />
  </ItemGroup>
</Project>
```

Add the project to `PriceNegotiationApp.slnx` under `/src/`.

- [ ] **Step 2: Domain**

Move with namespace change only: `Customer.cs`, `NegotiationStatus.cs`, `NegotiationOutcome.cs`, `NegotiationId.cs`, `CustomerId.cs`, `INegotiationPolicy.cs`, `DefaultNegotiationPolicy.cs` → namespace `PriceNegotiationApp.Modules.Negotiations.Domain`. Copy `Price.cs` verbatim into the module (Catalog keeps needing it too in Task 5 — duplication by design, spec §7).

Rewrite `Negotiation.cs` (new Start signature decouples from the Product aggregate):

```csharp
using PriceNegotiationApp.Modules.Negotiations.Domain;

namespace PriceNegotiationApp.Modules.Negotiations.Domain;

public sealed class Negotiation
{
    /// <summary>Base price snapshot taken at creation; protects ongoing negotiations from later product price changes.</summary>
    public decimal BasePrice { get; private set; }

    public decimal CurrentOffer { get; private set; }

    public NegotiationId Id { get; private set; }

    public Guid ProductId { get; private set; }

    public CustomerId CustomerId { get; private set; }

    public NegotiationStatus Status { get; private set; }

    /// <summary>Total proposals recorded, including the initial one.</summary>
    public int ProposalsUsed { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset LastProposalAtUtc { get; private set; }

    public DateTimeOffset? DecidedAtUtc { get; private set; }

    public uint Version { get; private set; }

    private Negotiation()
    {
    }

    private Negotiation(
        NegotiationId id, Guid productId, CustomerId customerId, decimal basePrice, decimal currentOffer,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        ProductId = productId;
        CustomerId = customerId;
        BasePrice = basePrice;
        CurrentOffer = currentOffer;
        Status = NegotiationStatus.Open;
        ProposalsUsed = 1;
        CreatedAtUtc = createdAtUtc;
        LastProposalAtUtc = createdAtUtc;
    }

    public static Negotiation Start(
        CustomerId customerId, Guid productId, decimal basePriceSnapshot, decimal initialOffer,
        DateTimeOffset now, INegotiationPolicy policy)
    {
        EnsureWithinLimit(basePriceSnapshot, initialOffer, policy);
        return new Negotiation(NegotiationId.From(Guid.CreateVersion7()), productId, customerId,
            basePriceSnapshot, initialOffer, now);
    }

    public NegotiationOutcome CounterPropose(decimal offer, DateTimeOffset now, INegotiationPolicy policy)
    {
        EnsureOpen();
        if (ProposalsUsed >= policy.MaxProposalsPerNegotiation)
        {
            return NegotiationOutcome.NoProposalsRemaining;
        }

        try
        {
            EnsureWithinLimit(BasePrice, offer, policy);
        }
        catch (ProposalExceedsLimitException)
        {
            Status = NegotiationStatus.Declined;
            DecidedAtUtc = now;
            return NegotiationOutcome.AutoRejected;
        }

        CurrentOffer = offer;
        ProposalsUsed++;
        LastProposalAtUtc = now;
        return NegotiationOutcome.CounterProposed;
    }

    public void Accept(DateTimeOffset now) => Decide(NegotiationStatus.Accepted, now);

    /// <summary>
    /// Staff rejects the current offer. The negotiation deliberately stays open so the
    /// customer may spend a remaining proposal; it terminates only via Accept,
    /// auto-rejection, or withdrawal.
    /// </summary>
    public void Decline() => EnsureOpen();

    public int RemainingProposals(INegotiationPolicy policy) =>
        Math.Max(0, policy.MaxProposalsPerNegotiation - ProposalsUsed);

    private void Decide(NegotiationStatus terminalStatus, DateTimeOffset now)
    {
        EnsureOpen();
        Status = terminalStatus;
        DecidedAtUtc = now;
    }

    private void EnsureOpen()
    {
        if (Status != NegotiationStatus.Open)
        {
            throw new ClosedNegotiationException();
        }
    }

    private static void EnsureWithinLimit(decimal basePrice, decimal offer, INegotiationPolicy policy)
    {
        var limit = decimal.Round(basePrice * policy.ProposalMultiplierLimit, 2);
        Price.From(offer);
        if (offer > limit)
        {
            throw new ProposalExceedsLimitException(limit);
        }
    }
}
```

Changes vs old: `ProductId` is `Guid` (not typed VO — foreign-module key), `Start` takes snapshot primitives, `Entity` base dropped (was stateless).

Create `Domain/NegotiationExceptions.cs` (merge of the two moved exception files; bodies verbatim otherwise):

```csharp
using PriceNegotiationApp.BuildingBlocks;

namespace PriceNegotiationApp.Modules.Negotiations.Domain;

/// <summary>Thrown when an operation targets a negotiation that has already reached a terminal state.</summary>
public sealed class ClosedNegotiationException()
    : DomainException("Negotiation is already closed.");
```

and keep `ProposalExceedsLimitException` as its own file `Domain/ProposalExceedsLimitException.cs` (body verbatim from Domain).

- [ ] **Step 3: Port**

`Ports/IProductPriceProvider.cs`:

```csharp
namespace PriceNegotiationApp.Modules.Negotiations.Ports;

public interface IProductPriceProvider
{
    /// <summary>Returns null when the product does not exist.</summary>
    Task<ProductSnapshot?> GetAsync(Guid productId, CancellationToken ct);
}

public readonly record struct ProductSnapshot(Guid ProductId, decimal Price);
```

- [ ] **Step 4: Persistence**

Move `NegotiationsDbContext.cs` from Infrastructure into `Modules.Negotiations/Persistence/` with namespace `PriceNegotiationApp.Modules.Negotiations.Persistence`; change configuration wiring to explicit `ApplyConfiguration(new ...)` referencing the moved configuration classes below.

Move + rewrite `Persistence/Configurations/CustomerConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceNegotiationApp.Modules.Negotiations.Domain;

namespace PriceNegotiationApp.Modules.Negotiations.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasConversion(id => id.Value, value => CustomerId.From(value))
            .ValueGeneratedNever();
        builder.HasIndex(c => c.IdentityUserId).IsUnique();
    }
}
```

Move + rewrite `Persistence/Configurations/NegotiationConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceNegotiationApp.Modules.Negotiations.Domain;

namespace PriceNegotiationApp.Modules.Negotiations.Persistence.Configurations;

public sealed class NegotiationConfiguration : IEntityTypeConfiguration<Negotiation>
{
    public void Configure(EntityTypeBuilder<Negotiation> builder)
    {
        builder.ToTable("negotiations");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasConversion(id => id.Value, value => NegotiationId.From(value))
            .ValueGeneratedNever();
        // Plain Guid keys: product_id has NO FK by design (separate schemas/modules).
        // Existence is validated at creation; negotiations survive deletion on snapshots.
        builder.Property(n => n.ProductId);
        builder.Property(n => n.CustomerId).HasConversion(id => id.Value, value => CustomerId.From(value));
        builder.Property(n => n.BasePrice).HasColumnType("numeric(18,2)");
        builder.Property(n => n.CurrentOffer).HasColumnType("numeric(18,2)");
        builder.Property(n => n.Status).HasConversion<int>();
        builder.HasOne<Customer>().WithMany().HasForeignKey(n => n.CustomerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(n => new { n.ProductId, n.CustomerId })
            .IsUnique()
            .HasFilter($"status = {(int)NegotiationStatus.Open}");
        builder.Property(n => n.Version).IsRowVersion();
    }
}
```

Create `Persistence/DesignTimeDbContextFactory.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PriceNegotiationApp.Modules.Negotiations.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<NegotiationsDbContext>
{
    public NegotiationsDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<NegotiationsDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=pricenego_design;Username=postgres;Password=postgres",
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Negotiations"))
            .UseSnakeCaseNamingConvention()
            .Options);
}
```

Generate fresh migrations in the module (history-table name unchanged; migration ids differ from Task 1's set because namespaces changed — disposable environments recreate; persistent environments additionally run `DELETE FROM "__EFMigrationsHistory_Negotiations";` once before first start — append this note to `docs/sql/cleanup-legacy-tables.sql`):

```powershell
Remove-Item -Recurse -Force src/PriceNegotiationApp.Infrastructure/Persistence/Migrations/Negotiations
dotnet ef migrations add Initial --context NegotiationsDbContext `
  -p src/PriceNegotiationApp.Modules.Negotiations -o Persistence/Migrations
```

Verify: no FK on `product_id`; FK `customer_id → negotiations.customers` cascade present; partial index present.

- [ ] **Step 5: Feature slices + models**

`Features/Negotiations/NegotiationModels.cs`:

```csharp
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Modules.Negotiations.Domain;

namespace PriceNegotiationApp.Modules.Negotiations.Features;

public sealed class CreateNegotiationRequest
{
    public Guid ProductId { get; init; }
}

public sealed class CounterProposalRequest
{
    public decimal ProposedPrice { get; init; }
}

public sealed record NegotiationResponse(
    Guid Id,
    Guid ProductId,
    decimal BasePrice,
    decimal CurrentOffer,
    string Status,
    int ProposalsUsed,
    int ProposalsRemaining,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastProposalAtUtc,
    DateTimeOffset? DecidedAtUtc);

public sealed record CounterProposalOutcome(string Outcome, NegotiationResponse Negotiation);

/// <summary>Machine-readable error codes owned by this feature (frozen contract).</summary>
public static class NegotiationErrorCodes
{
    public const string NegotiationClosed = "negotiation_closed";

    public const string ProposalExceedsLimit = "proposal_exceeds_limit";

    public const string NegotiationAlreadyOpen = "negotiation_already_open";

    public const string NoProposalsRemaining = "no_proposals_remaining";
}

internal static class NegotiationResponses
{
    internal static NegotiationResponse ToResponse(Negotiation n, INegotiationPolicy policy) =>
        new(n.Id.Value, n.ProductId, n.BasePrice, n.CurrentOffer, n.Status.ToString(),
            n.ProposalsUsed, n.RemainingProposals(policy), n.CreatedAtUtc, n.LastProposalAtUtc, n.DecidedAtUtc);
}
```

NOTE: `CreateNegotiationRequest` keeps property name `ProductId` (JSON shape unchanged). The original request had the same single property plus `ProposedPrice`; keep BOTH properties exactly as today:

```csharp
public sealed class CreateNegotiationRequest
{
    public Guid ProductId { get; init; }

    public decimal ProposedPrice { get; init; }
}
```

(use this second version — the first block above is superseded.)

`Features/Negotiations/NegotiationAccess.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.Modules.Negotiations.Persistence;

namespace PriceNegotiationApp.Modules.Negotiations.Features;

internal static class NegotiationAccess
{
    public static async Task<Negotiation> RequireAsync(NegotiationsDbContext db, Guid id, CancellationToken ct) =>
        await db.Negotiations.FirstOrDefaultAsync(n => n.Id.Value == id, ct)
        ?? throw new NotFoundException(nameof(Negotiation), id);

    public static async Task<Negotiation> RequireOwnedAsync(
        NegotiationsDbContext db, CallerContext caller, Guid id, CancellationToken ct)
    {
        var negotiation = await RequireAsync(db, id, ct);
        var customer = await CustomerByIdentityAsync(db, caller.UserId, ct);
        if (customer is null || customer.Id != negotiation.CustomerId)
        {
            throw new ForbiddenAccessException();
        }

        return negotiation;
    }

    public static async Task<bool> CanAccessAsync(
        NegotiationsDbContext db, CallerContext caller, Negotiation negotiation, CancellationToken ct)
    {
        if (caller.IsInRole(UserRoles.Admin) || caller.IsInRole(UserRoles.Staff))
        {
            return true;
        }

        var customer = await CustomerByIdentityAsync(db, caller.UserId, ct);
        return customer is not null && customer.Id == negotiation.CustomerId;
    }

    public static Task<Customer?> CustomerByIdentityAsync(
        NegotiationsDbContext db, Guid identityUserId, CancellationToken ct) =>
        db.Customers.FirstOrDefaultAsync(c => c.IdentityUserId == identityUserId, ct);

    public static async Task<CustomerId> GetOrCreateCustomerIdAsync(
        NegotiationsDbContext db, Guid identityUserId, CancellationToken ct)
    {
        var existing = await CustomerByIdentityAsync(db, identityUserId, ct);
        if (existing is not null)
        {
            return existing.Id;
        }

        var customer = Customer.Create(identityUserId);
        await db.Customers.AddAsync(customer, ct);
        return customer.Id;
    }

    public static Task<Negotiation?> FindOpenAsync(
        NegotiationsDbContext db, Guid productId, Guid identityUserId, CancellationToken ct) =>
        db.Negotiations.FirstOrDefaultAsync(
            n => n.ProductId == productId && n.Status == NegotiationStatus.Open, ct);
}
```

`FindOpenAsync` simplification is safe: `customer_id` matches only rows belonging to this identity's customer; a customer id is unique per identity user (unique index) so filtering on product+status alone could match ANOTHER customer's open negotiation — WRONG. Keep the join semantics of the original: filter `n.CustomerId == customer.Id` requires resolving the customer first:

```csharp
    public static async Task<Negotiation?> FindOpenAsync(
        NegotiationsDbContext db, Guid productId, Guid identityUserId, CancellationToken ct)
    {
        var customer = await CustomerByIdentityAsync(db, identityUserId, ct);
        return customer is null
            ? null
            : await db.Negotiations.FirstOrDefaultAsync(
                n => n.ProductId == productId && n.CustomerId == customer.Id && n.Status == NegotiationStatus.Open,
                ct);
    }
```

USE THIS SECOND VERSION (the first FindOpenAsync above is superseded and must not ship).

`Features/Negotiations/Create.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Modules.Identity.Public;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.Modules.Negotiations.Ports;
using PriceNegotiationApp.Modules.Negotiations.Persistence;

namespace PriceNegotiationApp.Modules.Negotiations.Features;

internal static class Create
{
    internal static RouteGroupBuilder MapCreate(this RouteGroupBuilder group) =>
        group.MapPost("/", async (CreateNegotiationRequest request, ClaimsPrincipal principal,
                NegotiationsDbContext db, IProductPriceProvider products, INegotiationPolicy policy,
                TimeProvider clock, CancellationToken ct) =>
            {
                var caller = principal.ToCallerContext();
                var snapshot = await products.GetAsync(request.ProductId, ct)
                               ?? throw new NotFoundException(nameof(Product), request.ProductId);

                if (await NegotiationAccess.FindOpenAsync(db, snapshot.ProductId, caller.UserId, ct) is not null)
                {
                    throw new ConflictException(NegotiationErrorCodes.NegotiationAlreadyOpen,
                        "An open negotiation already exists for this product.");
                }

                var customerId = await NegotiationAccess.GetOrCreateCustomerIdAsync(db, caller.UserId, ct);
                var negotiation = Negotiation.Start(customerId, snapshot.ProductId, snapshot.Price,
                    request.ProposedPrice, clock.GetUtcNow(), policy);
                await db.Negotiations.AddAsync(negotiation, ct);
                await db.SaveChangesAsync(ct);
                return TypedResults.Created("/api/v1/negotiations/mine",
                    NegotiationResponses.ToResponse(negotiation, policy));
            })
        .RequireRoles(UserRoles.Customer);
}
```

Until Task 6 lands `Modules.Identity.Public.UserRoles`, temporarily alias: add to the module root a file `Public/UserRoles.cs` NOW (it belongs to Identity ultimately — acceptable interim: create it in this task under `Modules.Negotiations/Public/UserRoles.cs` and MOVE it to Identity module in Task 6, fixing the using). Simpler alternative used everywhere in this plan: create `Modules.Identity/Public/UserRoles.cs` ALREADY in this task (create the Identity module folder early with just that file + minimal csproj? A csproj is needed for compilation…). Cleanest: create the FULL Identity module skeleton (csproj + Public/UserRoles.cs only) in this task; fleshed out in Task 6. Do that:

`src/PriceNegotiationApp.Modules.Identity/PriceNegotiationApp.Modules.Identity.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\..\src\PriceNegotiationApp.BuildingBlocks\PriceNegotiationApp.BuildingBlocks.csproj" />
  </ItemGroup>
</Project>
```

`src/PriceNegotiationApp.Modules.Identity/Public/UserRoles.cs`:

```csharp
namespace PriceNegotiationApp.Modules.Identity.Public;

public static class UserRoles
{
    public const string Admin = "Admin";

    public const string Staff = "Staff";

    public const string Customer = "Customer";
}
```

Add both projects to slnx. Delete `Application/Common/UserRoles.cs` and fix remaining consumers (`ProductsModule`, legacy services) to `using PriceNegotiationApp.Modules.Identity.Public;`.

Remaining seven operation files follow the identical pattern (route group + handler + `RequireAuthorization()`/`RequireRoles(...)` copied verbatim from the deleted `Api/Modules/NegotiationsModule.cs`). Full bodies:

`ListMine.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Modules.Identity.Public;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.Modules.Negotiations.Persistence;

namespace PriceNegotiationApp.Modules.Negotiations.Features;

internal static class ListMine
{
    internal static RouteGroupBuilder MapListMine(this RouteGroupBuilder group) =>
        group.MapGet("/mine", async (ClaimsPrincipal principal, NegotiationsDbContext db,
                INegotiationPolicy policy, CancellationToken ct, int page = 1, int pageSize = 20) =>
            {
                var caller = principal.ToCallerContext();
                var query = new PageQuery(page, pageSize);
                var customer = await NegotiationAccess.CustomerByIdentityAsync(db, caller.UserId, ct);
                var q = db.Negotiations.AsNoTracking().Where(n => customer != null && n.CustomerId == customer.Id);
                var total = await q.LongCountAsync(ct);
                var items = await q.OrderByDescending(n => n.CreatedAtUtc)
                    .Skip(query.Skip).Take(query.SafePageSize)
                    .ToListAsync(ct);
                return TypedResults.Ok(new PagedResult<NegotiationResponse>(
                    items.Select(n => NegotiationResponses.ToResponse(n, policy)).ToList(),
                    query.SafePage, query.SafePageSize, total));
            })
        .RequireRoles(UserRoles.Customer);
}
```

(`AsNoTracking`, `ToListAsync`, `LongCountAsync` require `using Microsoft.EntityFrameworkCore;` — included via the Persistence using? No: add explicit `using Microsoft.EntityFrameworkCore;` to every handler file that queries. Add it to each file below.)

`List.cs` (staff/admin listing all):

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Modules.Identity.Public;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.Modules.Negotiations.Persistence;

namespace PriceNegotiationApp.Modules.Negotiations.Features;

internal static class List
{
    internal static RouteGroupBuilder MapList(this RouteGroupBuilder group) =>
        group.MapGet("/", async (NegotiationsDbContext db, INegotiationPolicy policy,
                CancellationToken ct, int page = 1, int pageSize = 20) =>
            {
                var query = new PageQuery(page, pageSize);
                var q = db.Negotiations.AsNoTracking();
                var total = await q.LongCountAsync(ct);
                var items = await q.OrderByDescending(n => n.CreatedAtUtc)
                    .Skip(query.Skip).Take(query.SafePageSize)
                    .ToListAsync(ct);
                return TypedResults.Ok(new PagedResult<NegotiationResponse>(
                    items.Select(n => NegotiationResponses.ToResponse(n, policy)).ToList(),
                    query.SafePage, query.SafePageSize, total));
            })
        .RequireRoles(UserRoles.Admin, UserRoles.Staff);
}
```

`Get.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.Modules.Negotiations.Persistence;

namespace PriceNegotiationApp.Modules.Negotiations.Features;

internal static class Get
{
    internal static RouteGroupBuilder MapGetOne(this RouteGroupBuilder group) =>
        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal principal, NegotiationsDbContext db,
                INegotiationPolicy policy, CancellationToken ct) =>
            {
                var caller = principal.ToCallerContext();
                var negotiation = await NegotiationAccess.RequireAsync(db, id, ct);
                if (!await NegotiationAccess.CanAccessAsync(db, caller, negotiation, ct))
                {
                    throw new ForbiddenAccessException();
                }

                return TypedResults.Ok(NegotiationResponses.ToResponse(negotiation, policy));
            })
        .RequireAuthorization();
}
```

`CounterPropose.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.Modules.Negotiations.Persistence;

namespace PriceNegotiationApp.Modules.Negotiations.Features;

internal static class CounterPropose
{
    internal static RouteGroupBuilder MapCounterPropose(this RouteGroupBuilder group) =>
        group.MapPatch("/{id:guid}/proposals", async (Guid id, CounterProposalRequest request,
                ClaimsPrincipal principal, NegotiationsDbContext db, INegotiationPolicy policy,
                TimeProvider clock, CancellationToken ct) =>
            {
                var caller = principal.ToCallerContext();
                var negotiation = await NegotiationAccess.RequireOwnedAsync(db, caller, id, ct);

                var outcome = negotiation.CounterPropose(request.ProposedPrice, clock.GetUtcNow(), policy);
                if (outcome == NegotiationOutcome.NoProposalsRemaining)
                {
                    throw new ConflictException(NegotiationErrorCodes.NoProposalsRemaining,
                        "No proposals remain for this negotiation.");
                }

                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(new CounterProposalOutcome(outcome.ToString(),
                    NegotiationResponses.ToResponse(negotiation, policy)));
            })
        .RequireAuthorization();
}
```

`Accept.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Modules.Identity.Public;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.Modules.Negotiations.Persistence;

namespace PriceNegotiationApp.Modules.Negotiations.Features;

internal static class Accept
{
    internal static RouteGroupBuilder MapAccept(this RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/accept", async (Guid id, NegotiationsDbContext db,
                INegotiationPolicy policy, TimeProvider clock, CancellationToken ct) =>
            {
                var negotiation = await NegotiationAccess.RequireAsync(db, id, ct);
                negotiation.Accept(clock.GetUtcNow());
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(NegotiationResponses.ToResponse(negotiation, policy));
            })
        .RequireRoles(UserRoles.Admin, UserRoles.Staff);
}
```

`Decline.cs`: identical to `Accept` except method name `MapDecline`, route `"/{id:guid}/decline"`, and body:

```csharp
                negotiation.Decline();
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(NegotiationResponses.ToResponse(negotiation, policy));
```

`Withdraw.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Modules.Negotiations.Persistence;

namespace PriceNegotiationApp.Modules.Negotiations.Features;

internal static class Withdraw
{
    internal static RouteGroupBuilder MapWithdraw(this RouteGroupBuilder group) =>
        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal principal, NegotiationsDbContext db,
                CancellationToken ct) =>
            {
                var caller = principal.ToCallerContext();
                var negotiation = await NegotiationAccess.RequireAsync(db, id, ct);
                if (!caller.IsInRole(UserRoles.Admin)
                    && !await NegotiationAccess.CanAccessAsync(db, caller, negotiation, ct))
                {
                    throw new ForbiddenAccessException();
                }

                db.Negotiations.Remove(negotiation);
                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
        .RequireAuthorization();
}
```

- [ ] **Step 6: Module composition class**

`NegotiationsModule.cs` (module root):

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.Modules.Negotiations.Features;
using PriceNegotiationApp.Modules.Negotiations.Persistence;

namespace PriceNegotiationApp.Modules.Negotiations;

public static class NegotiationsModule
{
    public static IServiceCollection AddNegotiationsModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<NegotiationsDbContext>(options => options
            .UseNpgsql(DbConnections.Resolve(configuration, "Negotiations"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Negotiations"))
            .UseSnakeCaseNamingConvention());
        services.AddSingleton<INegotiationPolicy, DefaultNegotiationPolicy>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }

    public static IEndpointRouteBuilder MapNegotiationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/negotiations").WithTags("Negotiations");
        group.MapCreate();
        group.MapListMine();
        group.MapList();
        group.MapGetOne();
        group.MapCounterPropose();
        group.MapAccept();
        group.MapDecline();
        group.MapWithdraw();
        return app;
    }
}
```

This uses `DbConnections.Resolve` from BuildingBlocks — created in Task 3's corrective addendum (see Task 3a). If executing strictly sequentially, implement Task 3a before this step.

- [ ] **Step 7: Host adapter (over Infrastructure's CatalogDbContext until Task 5)**

`src/PriceNegotiationApp.Api/Composition/CatalogToNegotiations.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Infrastructure.Persistence;
using PriceNegotiationApp.Modules.Negotiations.Ports;

namespace PriceNegotiationApp.Api.Composition;

/// <summary>The single sanctioned inter-module edge: Negotiations reads product price snapshots.</summary>
public sealed class CatalogToNegotiations(CatalogDbContext db) : IProductPriceProvider
{
    public async Task<ProductSnapshot?> GetAsync(Guid productId, CancellationToken ct) =>
        await db.Products.AsNoTracking()
            .Where(p => p.Id.Value == productId)
            .Select(p => new ProductSnapshot(p.Id.Value, p.Price))
            .FirstOrDefaultAsync(ct);
}
```

- [ ] **Step 8: Host wiring updates**

In `WebApplicationBuilderExtensions.AddApiServices` replace `services.AddApplicationServices(); services.AddInfrastructure(configuration);` region: remove the Application registrations for negotiations (`INegotiationService`) — concretely, delete from `Application/DependencyInjection.cs` the lines registering `INegotiationService` — and add to the Api composition:

```csharp
builder.Services.AddNegotiationsModule(configuration);
builder.Services.AddScoped<IProductPriceProvider, CatalogToNegotiations>();
```

In `PipelineExtensions.MapModules` replace `app.MapNegotiationsApi();` with `app.MapNegotiationsEndpoints();`.

Update `GlobalExceptionHandler.cs` usings: `using PriceNegotiationApp.Modules.Negotiations.Domain;` (for Closed/Proposal exceptions) and `using PriceNegotiationApp.Modules.Negotiations.Features;` (for `NegotiationErrorCodes.NegotiationClosed` / `.ProposalExceedsLimit`). The switch arms stay textually identical.

- [ ] **Step 9: Deletions**

Delete the files listed at the top of this task (`NegotiationService.cs`, `INegotiationService.cs`, both repository ports/impls, old endpoint module, old domain files, old Infrastructure `NegotiationsDbContext.cs`, `NegotiationServiceShould.cs`). Fix any compile errors by following the Namespace Migration Map.

- [ ] **Step 10: Module unit-test project**

`tests/PriceNegotiationApp.Modules.Negotiations.Tests/PriceNegotiationApp.Modules.Negotiations.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <NoWarn>$(NoWarn);CA1707;S1118</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\PriceNegotiationApp.Modules.Negotiations\PriceNegotiationApp.Modules.Negotiations.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Bogus" />
    <PackageReference Include="Shouldly" />
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>
</Project>
```

Move `NegotiationLifecycleShould.cs` + `PriceShould.cs` from UnitTests with namespace `PriceNegotiationApp.Modules.Negotiations.Tests` and usings remapped to `PriceNegotiationApp.Modules.Negotiations.Domain`. Update every `Negotiation.Start(...)` call site to the new signature — pattern:

```csharp
// old
var product = Product.Create("Widget", 100m);
var sut = Negotiation.Start(customerId, product, 90m, now, Policy);
// new
var sut = Negotiation.Start(customerId, productId: Guid.NewGuid(), basePriceSnapshot: 100m,
    initialOffer: 90m, now, Policy);
```

(`Customer.Create(identityUserId)` unchanged; construct `CustomerId` directly where tests built products solely to feed `Start`.) Delete `NegotiationServiceShould.cs` (its subject is gone; branches covered by the integration RBAC matrix). Add the test project to slnx.

- [ ] **Step 11: Validate**

```powershell
dotnet build PriceNegotiationApp.slnx
dotnet test tests/PriceNegotiationApp.Modules.Negotiations.Tests
dotnet test tests/PriceNegotiationApp.UnitTests
dotnet test tests/PriceNegotiationApp.IntegrationTests
```

Full suite green — negotiation flows byte-identical over HTTP.

- [ ] **Step 12: Commit**

```bash
git add -A && git commit -m "refactor(modules): carve out Negotiations module with own context and port"
```


---

### Task 5: Carve out the Catalog module

Same motion as Task 4 for the smaller context. Also relocates `MigrationHostedService` to the AppHost (it must see all three contexts, two of which are now module types).

**Files:**
- Create: `src/PriceNegotiationApp.Modules.Catalog/**` (files below)
- Create: `tests/PriceNegotiationApp.Modules.Catalog.Tests/**`
- Move: `Infrastructure/Hosting/MigrationHostedService.cs` → `src/PriceNegotiationApp.Api/Composition/MigrationHostedService.cs` (namespace `PriceNegotiationApp.Api.Composition`)
- Modify: `WebApplicationBuilderExtensions.cs`, `PipelineExtensions.cs`, Api `Composition/CatalogToNegotiations.cs` (using swap)
- Modify: `PriceNegotiationApp.slnx`
- Delete: `Domain/Models/Product.cs`, `Domain/Models/Rules/*`, `Domain/ValueObjects/Price.cs`, `Domain/ValueObjects/Ids/ProductId.cs`, `Application/Features/Products/*`, `Application/Abstractions/IProductRepository.cs`, `Infrastructure/Persistence/Repositories/ProductRepository.cs`, `Api/Modules/ProductsModule.cs`, `Infrastructure/Persistence/CatalogDbContext.cs`, `DbEntityConfigurations/ProductConfiguration.cs`, `Persistence/Migrations/Catalog/`, `Infrastructure/Hosting/*`, `Infrastructure/Seeding/CatalogSeedingHostedService.cs`, `tests/...UnitTests/Application/ProductServiceShould.cs`

**Interfaces:**
- Consumes: BuildingBlocks (`Policies.ShortCachePolicy`, `PageQuery`, `PagedResult`, `ProductQuery`, exceptions).
- Produces:
  - `CatalogModule.AddCatalogModule(this IServiceCollection, IConfiguration)` / `.MapCatalogEndpoints(this IEndpointRouteBuilder)`
  - `ProductResponse(Guid Id, string Name, decimal Price)` (JSON unchanged)

**Step 1 — Project file**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <!-- Vogen-generated IComparable implementations trigger MA0097 on the declaring partial. -->
    <NoWarn>$(NoWarn);MA0097</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\PriceNegotiationApp.BuildingBlocks\PriceNegotiationApp.BuildingBlocks.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="EFCore.NamingConventions" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
    <PackageReference Include="Vogen" />
  </ItemGroup>
</Project>
```

**Step 2 — Domain**

Copy `Price.cs` verbatim → namespace `PriceNegotiationApp.Modules.Catalog.Domain`. Move `ProductId.cs` verbatim → same namespace.

Rewrite `Domain/Product.cs` (inline guards replace the rule classes; behavior identical):

```csharp
using PriceNegotiationApp.BuildingBlocks;
using Vogen;

namespace PriceNegotiationApp.Modules.Catalog.Domain;

public sealed class Product
{
    public const int MaxNameLength = 200;

    public ProductId Id { get; private set; }

    public string Name { get; private set; } = null!;

    public decimal Price { get; private set; }

    /// <summary>Optimistic-concurrency token mapped to PostgreSQL xmin.</summary>
    public uint Version { get; private set; }

    private Product()
    {
    }

    private Product(ProductId id, string name, decimal price)
    {
        EnsureValid(name, price);
        Id = id;
        Name = name.Trim();
        Price = Price.From(price).Value;
    }

    public static Product Create(string name, decimal price) =>
        new(ProductId.From(Guid.CreateVersion7()), name, price);

    /// <summary>Applies changes. Returns false when nothing changed (PUT stays idempotent).</summary>
    public bool Update(string name, decimal price)
    {
        EnsureValid(name, price);
        var validated = Price.From(price).Value;
        var trimmed = name.Trim();
        if (string.Equals(Name, trimmed, StringComparison.Ordinal) && Price == validated)
        {
            return false;
        }

        Name = trimmed;
        Price = validated;
        return true;
    }

    private static void EnsureValid(string? name, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Product name must not be empty.");
        }

        if (name.Trim().Length > MaxNameLength)
        {
            throw new DomainException($"Product name must not exceed {MaxNameLength} characters.");
        }

        Price.From(price);
    }
}
```

(Positivity enforced through the `Price` value object — Vogen's `ValueObjectValidationException` → 422; empty/too-long name → `BuildingBlocks.DomainException` → 422 `domain_rule_violated`. Semantics identical to the deleted rule classes.)

**Step 3 — Persistence**

Move + namespace-swap `CatalogDbContext.cs` → `Modules.Catalog/Persistence/` (`PriceNegotiationApp.Modules.Catalog.Persistence`); its configuration registration points at the moved configuration below.

Move `ProductConfiguration.cs` → `Persistence/Configurations/ProductConfiguration.cs`, namespace `PriceNegotiationApp.Modules.Catalog.Persistence.Configurations` (body otherwise verbatim).

Create `Persistence/DesignTimeDbContextFactory.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PriceNegotiationApp.Modules.Catalog.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=pricenego_design;Username=postgres;Password=postgres",
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Catalog"))
            .UseSnakeCaseNamingConvention()
            .Options);
}
```

Regenerate migrations:

```powershell
Remove-Item -Recurse -Force src/PriceNegotiationApp.Infrastructure/Persistence/Migrations/Catalog
dotnet ef migrations add Initial --context CatalogDbContext `
  -p src/PriceNegotiationApp.Modules.Catalog -o Persistence/Migrations
```

Append to `docs/sql/cleanup-legacy-tables.sql`: `DELETE FROM "__EFMigrationsHistory_Catalog";` (one-time, before first start — migration ids changed with the assembly move).

**Step 4 — Seeding moves into the module**

Create `Seeding/CatalogSeedingOptions.cs`:

```csharp
namespace PriceNegotiationApp.Modules.Catalog.Seeding;

public sealed class CatalogSeedingOptions
{
    public const string SectionName = "Seeding";

    public bool SeedSampleProducts { get; init; }
}
```

Move the seeder from Infrastructure → `Seeding/CatalogSeedingHostedService.cs` (namespace `PriceNegotiationApp.Modules.Catalog.Seeding`), changing only: constructor option type to `IOptions<CatalogSeedingOptions>`, context using to the module namespace; body as written in Task 2 Step 4. Delete the Infrastructure copy.

**Step 5 — Feature slices**

`Features/Products/ProductModels.cs`:

```csharp
namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

public sealed class CreateProductRequest
{
    public string Name { get; init; } = string.Empty;

    public decimal Price { get; init; }
}

public sealed class UpdateProductRequest
{
    public string Name { get; init; } = string.Empty;

    public decimal Price { get; init; }
}

public sealed record ProductResponse(Guid Id, string Name, decimal Price);
```

`Features/Products/List.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Modules.Catalog.Persistence;

namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

internal static class List
{
    internal static RouteGroupBuilder MapList(this RouteGroupBuilder group) =>
        group.MapGet("/", async (CatalogDbContext db, CancellationToken ct,
                string? search = null, decimal? minPrice = null, decimal? maxPrice = null,
                string? sortBy = null, bool sortDesc = false, int page = 1, int pageSize = 20) =>
                TypedResults.Ok(await SearchAsync(db,
                    new ProductQuery(search, minPrice, maxPrice, sortBy, sortDesc, page, pageSize), ct)))
            .CacheOutput(Policies.ShortCachePolicy)
            .AllowAnonymous();

    internal static async Task<PagedResult<ProductResponse>> SearchAsync(
        CatalogDbContext db, ProductQuery query, CancellationToken ct)
    {
        var page = new PageQuery(query.Page, query.PageSize);
        var q = db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            q = q.Where(p => EF.Functions.ILike(p.Name, $"%{query.Search.Trim()}%"));
        }

        if (query.MinPrice.HasValue)
        {
            q = q.Where(p => p.Price >= query.MinPrice.Value);
        }

        if (query.MaxPrice.HasValue)
        {
            q = q.Where(p => p.Price <= query.MaxPrice.Value);
        }

        var sortBy = query.SortBy?.Trim().ToLowerInvariant();
        q = (sortBy, query.SortDesc) switch
        {
            ("price", false) => q.OrderBy(p => p.Price),
            ("price", true) => q.OrderByDescending(p => p.Price),
            (_, true) => q.OrderByDescending(p => p.Name),
            _ => q.OrderBy(p => p.Name),
        };

        var total = await q.LongCountAsync(ct);
        var items = await q
            .Skip(page.Skip)
            .Take(page.SafePageSize)
            .Select(p => new ProductResponse(p.Id.Value, p.Name, p.Price))
            .ToListAsync(ct);

        return new PagedResult<ProductResponse>(items, page.SafePage, page.SafePageSize, total);
    }
}
```

`Features/Products/Get.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Modules.Catalog.Domain;
using PriceNegotiationApp.Modules.Catalog.Persistence;

namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

internal static class Get
{
    internal static RouteGroupBuilder MapGetOne(this RouteGroupBuilder group) =>
        group.MapGet("/{id:guid}", async (Guid id, CatalogDbContext db, CancellationToken ct) =>
                TypedResults.Ok(await RequireAsync(db, id, ct)))
            .WithName("GetProductById")
            .CacheOutput(Policies.ShortCachePolicy)
            .AllowAnonymous();

    internal static async Task<ProductResponse> RequireAsync(CatalogDbContext db, Guid id, CancellationToken ct) =>
        await db.Products.AsNoTracking()
            .Where(p => p.Id.Value == id)
            .Select(p => new ProductResponse(p.Id.Value, p.Name, p.Price))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException(nameof(Product), id);
}
```

`Features/Products/Create.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Modules.Catalog.Domain;
using PriceNegotiationApp.Modules.Catalog.Persistence;
using PriceNegotiationApp.Modules.Identity.Public;

namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

internal static class Create
{
    internal static RouteGroupBuilder MapCreate(this RouteGroupBuilder group) =>
        group.MapPost("/", async (CreateProductRequest request, CatalogDbContext db, CancellationToken ct) =>
            {
                var product = Product.Create(request.Name, request.Price);
                await db.Products.AddAsync(product, ct);
                await db.SaveChangesAsync(ct);
                return TypedResults.CreatedAtRoute(
                    new ProductResponse(product.Id.Value, product.Name, product.Price),
                    "GetProductById", new { id = product.Id.Value });
            })
        .RequireRoles(UserRoles.Admin, UserRoles.Staff);
}
```

`Features/Products/Update.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Modules.Catalog.Domain;
using PriceNegotiationApp.Modules.Catalog.Persistence;
using PriceNegotiationApp.Modules.Identity.Public;

namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

internal static class Update
{
    internal static RouteGroupBuilder MapUpdate(this RouteGroupBuilder group) =>
        group.MapPut("/{id:guid}", async (Guid id, UpdateProductRequest request, CatalogDbContext db,
                CancellationToken ct) =>
            {
                var product = await db.Products.FirstOrDefaultAsync(p => p.Id.Value == id, ct)
                              ?? throw new NotFoundException(nameof(Product), id);
                product.Update(request.Name, request.Price);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(new ProductResponse(product.Id.Value, product.Name, product.Price));
            })
        .RequireRoles(UserRoles.Admin, UserRoles.Staff);
}
```

`Features/Products/Delete.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Modules.Catalog.Persistence;
using PriceNegotiationApp.Modules.Identity.Public;

namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

internal static class Delete
{
    internal static RouteGroupBuilder MapDelete(this RouteGroupBuilder group) =>
        group.MapDelete("/{id:guid}", async (Guid id, CatalogDbContext db, CancellationToken ct) =>
            {
                var product = await db.Products.FirstOrDefaultAsync(p => p.Id.Value == id, ct)
                              ?? throw new NotFoundException(nameof(Product), id);
                // Negotiations survive on their snapshots by design (spec §6).
                db.Products.Remove(product);
                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
        .RequireRoles(UserRoles.Admin);
}
```

**Step 6 — Module composition**

`CatalogModule.cs` (module root):

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Modules.Catalog.Features.Products;
using PriceNegotiationApp.Modules.Catalog.Persistence;
using PriceNegotiationApp.Modules.Catalog.Seeding;

namespace PriceNegotiationApp.Modules.Catalog;

public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CatalogDbContext>(options => options
            .UseNpgsql(DbConnections.Resolve(configuration, "Catalog"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Catalog"))
            .UseSnakeCaseNamingConvention());
        services.AddOptions<CatalogSeedingOptions>()
            .Bind(configuration.GetSection(CatalogSeedingOptions.SectionName));
        services.AddHostedService<CatalogSeedingHostedService>();
        return services;
    }

    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/products").WithTags("Products");
        group.MapList();
        group.MapGetOne();
        group.MapCreate();
        group.MapUpdate();
        group.MapDelete();
        return app;
    }
}
```

Remove from `Infrastructure/DependencyInjection.cs`: the `AddDbContext<CatalogDbContext>` block, the `IProductRepository` scoped registration, and the transitional `CatalogSeedingHostedService` registration.

**Step 7 — Host wiring updates**

`WebApplicationBuilderExtensions.AddApiServices`: add `builder.Services.AddCatalogModule(configuration);` next to the Negotiations call. `PipelineExtensions`: `app.MapProductsApi();` → `app.MapCatalogEndpoints();`.

Move `MigrationHostedService` to `src/PriceNegotiationApp.Api/Composition/MigrationHostedService.cs` (host references every module; Infrastructure no longer does). Keep the Identity context using pointing at `PriceNegotiationApp.Infrastructure.Persistence` until Task 6 swaps it:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PriceNegotiationApp.Infrastructure.Persistence;
using PriceNegotiationApp.Modules.Catalog.Persistence;
using PriceNegotiationApp.Modules.Negotiations.Persistence;

namespace PriceNegotiationApp.Api.Composition;

public sealed class MigrationHostedService(IServiceScopeFactory scopeFactory, ILogger<MigrationHostedService> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        foreach (var contextType in new[]
                 {
                     typeof(IdentityModuleDbContext),
                     typeof(CatalogDbContext),
                     typeof(NegotiationsDbContext),
                 })
        {
            var db = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);
            await db.Database.MigrateAsync(cancellationToken);
        }

        logger.LogInformation("Module databases migrated.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

Register it in `AddApiServices` via `builder.Services.AddHostedService<MigrationHostedService>();` placed BEFORE module registrations (seeders must run after migrations); delete the Infrastructure copy and its DI registration.

Adapter rewire — `Composition/CatalogToNegotiations.cs` using change only:

```csharp
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Modules.Catalog.Persistence;
using PriceNegotiationApp.Modules.Negotiations.Ports;
```

**Step 8 — Deletions + test project**

Delete everything listed at the top of this task. Delete `ProductServiceShould.cs`.

`tests/PriceNegotiationApp.Modules.Catalog.Tests/PriceNegotiationApp.Modules.Catalog.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <NoWarn>$(NoWarn);CA1707;S1118</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\PriceNegotiationApp.Modules.Catalog\PriceNegotiationApp.Modules.Catalog.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Bogus" />
    <PackageReference Include="Shouldly" />
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>
</Project>
```

Move `ProductRulesShould.cs` here (namespace `PriceNegotiationApp.Modules.Catalog.Tests`; usings remapped per map; `DomainException` assertions stay valid). Port the PUT no-op guard into `UpdateIdempotencyShould.cs`:

```csharp
using Bogus;
using PriceNegotiationApp.Modules.Catalog.Domain;
using Shouldly;
using Xunit;

namespace PriceNegotiationApp.Modules.Catalog.Tests;

public class UpdateIdempotencyShould
{
    private static readonly Faker Faker = new();

    [Fact]
    public void ReturnFalseWhenNothingChanged()
    {
        var name = Faker.Commerce.ProductName();
        var price = Faker.Random.Decimal(1m, 1_000m);
        var product = Product.Create(name, price);

        var changed = product.Update(name, price);

        changed.ShouldBeFalse();
    }

    [Fact]
    public void ReturnTrueWhenOnlyWhitespaceDiffers()
    {
        var padded = $"{Faker.Commerce.ProductName()}   ";
        var product = Product.Create(Faker.Commerce.ProductName(), 10m);

        var changed = product.Update(padded, 10m);

        changed.ShouldBeTrue();
        product.Name.ShouldBe(padded.Trim());
    }
}
```

Add both projects to slnx.

**Step 9 — Validate**

```powershell
dotnet build PriceNegotiationApp.slnx
dotnet test tests/PriceNegotiationApp.Modules.Catalog.Tests
dotnet test tests/PriceNegotiationApp.UnitTests
dotnet test tests/PriceNegotiationApp.IntegrationTests
```

Full suite green (product matrix byte-identical, including filter/sort/page and cache headers).

**Step 10 — Commit**

```bash
git add -A && git commit -m "refactor(modules): carve out Catalog module with own context"
```


---

### Task 6: Carve out the Identity module; legacy projects die

Identity is the last carve-out. When it lands, `Application`, `Domain`, and `Infrastructure` are empty shells and are deleted here — not in a separate cleanup task — because nothing references them anymore.

**Files:**
- Create: everything under `src/PriceNegotiationApp.Modules.Identity/**` not already present (csproj exists since Task 4)
- Create: `tests/PriceNegotiationApp.Modules.Identity.Tests/**`
- Modify: `WebApplicationBuilderExtensions.cs`, `PipelineExtensions.cs`, `GlobalExceptionHandler.cs` (usings), Api `.csproj` (drop Application/Infrastructure references)
- Delete: `src/PriceNegotiationApp.Application/`, `src/PriceNegotiationApp.Domain/`, `src/PriceNegotiationApp.Infrastructure/` (whole projects), their slnx entries, `Infrastructure/Data/DesignTimeFactories.cs` (superseded by per-module factories)
- Modify: `PriceNegotiationApp.slnx`

**Interfaces:**
- Consumes: BuildingBlocks (`Policies`, exceptions, `CallerContextExtensions.ToCallerContext`).
- Produces:
  - `IdentityModule.AddIdentityModule(this IServiceCollection, IConfiguration)` / `.MapAuthEndpoints(this IEndpointRouteBuilder)`
  - `Modules.Identity.Auth.JwtManager` with `Task<(string Token, DateTimeOffset ExpiresAtUtc)> GenerateAsync(Guid userId, string email, IReadOnlyCollection<string> roles)`
  - `Modules.Identity.Public.IdentityErrorCodes`: `EmailAlreadyRegistered="email_already_registered"`, `InvalidCredentials="invalid_credentials"`, `AccountLocked="account_locked"`, `RegistrationInvalid="registration_invalid"`
  - `Modules.Identity.Seeding.SeedingOptions` (same `"Seeding"` section shape as today)

**Step 1 — Complete the csproj**

Replace the Task 4 skeleton with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\PriceNegotiationApp.BuildingBlocks\PriceNegotiationApp.BuildingBlocks.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="EFCore.NamingConventions" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />
    <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
    <PackageReference Include="System.IdentityModel.Tokens.Jwt" />
  </ItemGroup>
</Project>
```

**Step 2 — Public contracts**

`Public/IdentityErrorCodes.cs`:

```csharp
namespace PriceNegotiationApp.Modules.Identity.Public;

public static class IdentityErrorCodes
{
    public const string EmailAlreadyRegistered = "email_already_registered";

    public const string InvalidCredentials = "invalid_credentials";

    public const string AccountLocked = "account_locked";

    public const string RegistrationInvalid = "registration_invalid";
}
```

(`Public/UserRoles.cs` already exists from Task 4.)

**Step 3 — Auth**

Move verbatim (namespace → `PriceNegotiationApp.Modules.Identity.Auth`): `JwtOptions.cs`, `JwtOptionsValidator.cs`. Move `JwtManager.cs` with one change — drop the `IJwtTokenGenerator` implementation (the port dies with Application); constructor and body otherwise identical:

```csharp
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PriceNegotiationApp.Modules.Identity.Auth;

public sealed class JwtManager(IOptions<JwtOptions> options, TimeProvider clock)
{
    public Task<(string Token, DateTimeOffset ExpiresAtUtc)> GenerateAsync(
        Guid userId, string email, IReadOnlyCollection<string> roles)
    {
        // body identical to the current Infrastructure/Auth/JwtManager.cs implementation
        // (claims: sub, email, jti=Guid.CreateVersion7(), role per role; HS256; notBefore/expires from clock)
    }
}
```

(Copy the body from the existing file — do not retype it from this sketch.)

**Step 4 — Persistence**

Move `ApplicationUser.cs` → `Persistence/ApplicationUser.cs` (namespace `PriceNegotiationApp.Modules.Identity.Persistence`).

Move `IdentityModuleDbContext.cs` (created in Task 1) → same folder, namespace `PriceNegotiationApp.Modules.Identity.Persistence`.

Create `Persistence/DesignTimeDbContextFactory.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PriceNegotiationApp.Modules.Identity.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<IdentityModuleDbContext>
{
    public IdentityModuleDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<IdentityModuleDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=pricenego_design;Username=postgres;Password=postgres",
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Identity"))
            .UseSnakeCaseNamingConvention()
            .Options);
}
```

Regenerate migrations:

```powershell
dotnet ef migrations add Initial --context IdentityModuleDbContext `
  -p src/PriceNegotiationApp.Modules.Identity -o Persistence/Migrations
```

(Append `DELETE FROM "__EFMigrationsHistory_Identity";` to the cleanup-script notes.)

**Step 5 — Seeding**

Move `SeedingOptions.cs` verbatim (namespace → `PriceNegotiationApp.Modules.Identity.Seeding`). Move `IdentitySeedingHostedService.cs` from Infrastructure into the module, swapping its usings to the module namespaces (`UserRoles` → `PriceNegotiationApp.Modules.Identity.Public`, user types → `...Identity.Persistence`). Delete the Infrastructure copy and its DI registration.

**Step 6 — Feature slices**

`Features/Auth/AuthModels.cs`:

```csharp
namespace PriceNegotiationApp.Modules.Identity.Features.Auth;

public sealed class RegisterRequest
{
    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}

public sealed class LoginRequest
{
    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}

public sealed record RegistrationResponse(Guid UserId);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    string Email,
    IReadOnlyList<string> Roles);

public sealed record CurrentUserResponse(Guid UserId, string Email, IReadOnlyList<string> Roles);
```

`Features/Auth/Register.cs` (logic merged verbatim from `AuthService.RegisterAsync` + `IdentityAccountStore.RegisterAsync`):

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Modules.Identity.Persistence;
using PriceNegotiationApp.Modules.Identity.Public;

namespace PriceNegotiationApp.Modules.Identity.Features.Auth;

internal static class Register
{
    internal static RouteGroupBuilder MapRegister(this RouteGroupBuilder group) =>
        group.MapPost("/register", async (RegisterRequest request,
                UserManager<ApplicationUser> userManager, CancellationToken ct) =>
            {
                var user = new ApplicationUser { UserName = request.Email, Email = request.Email };
                var result = await userManager.CreateAsync(user, request.Password);
                if (!result.Succeeded)
                {
                    if (result.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
                    {
                        throw new ConflictException(IdentityErrorCodes.EmailAlreadyRegistered,
                            "Email already registered.");
                    }

                    throw new InvalidRequestException(IdentityErrorCodes.RegistrationInvalid,
                        string.Join("; ", result.Errors.Select(e => e.Description)));
                }

                await userManager.AddToRoleAsync(user, UserRoles.Customer);
                return TypedResults.Created("/api/v1/auth/me", new RegistrationResponse(user.Id));
            })
        .RequireRateLimiting(Policies.AuthRateLimitPolicy)
        .AllowAnonymous();
}
```

`Features/Auth/Login.cs` (merged verbatim from `AuthService.LoginAsync` + `IdentityAccountStore.PasswordSignInAsync`; the original's post-sign-in `FindByEmailAsync` round-trip collapses onto the loaded user — externally identical):

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Modules.Identity.Auth;
using PriceNegotiationApp.Modules.Identity.Persistence;
using PriceNegotiationApp.Modules.Identity.Public;

namespace PriceNegotiationApp.Modules.Identity.Features.Auth;

internal static class Login
{
    internal static RouteGroupBuilder MapLogin(this RouteGroupBuilder group) =>
        group.MapPost("/login", async (LoginRequest request, UserManager<ApplicationUser> userManager,
                JwtManager jwt, CancellationToken ct) =>
            {
                var user = await userManager.FindByNameAsync(request.Email)
                           ?? throw new UnauthorizedException(
                               IdentityErrorCodes.InvalidCredentials, "Invalid credentials.");

                if (await userManager.IsLockedOutAsync(user))
                {
                    throw new UnauthorizedException(IdentityErrorCodes.AccountLocked,
                        "Account temporarily locked.");
                }

                if (!await userManager.CheckPasswordAsync(user, request.Password))
                {
                    await userManager.AccessFailedAsync(user);
                    throw await userManager.IsLockedOutAsync(user)
                        ? new UnauthorizedException(IdentityErrorCodes.AccountLocked,
                            "Account temporarily locked.")
                        : new UnauthorizedException(IdentityErrorCodes.InvalidCredentials,
                            "Invalid credentials.");
                }

                await userManager.ResetAccessFailedCountAsync(user);

                var roles = (IReadOnlyList<string>)await userManager.GetRolesAsync(user);
                var (token, expiresAtUtc) = await jwt.GenerateAsync(user.Id, request.Email, roles);
                return TypedResults.Ok(new AuthResponse(token, expiresAtUtc, request.Email, roles));
            })
        .RequireRateLimiting(Policies.AuthRateLimitPolicy)
        .AllowAnonymous();
}
```

`Features/Auth/Me.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using PriceNegotiationApp.BuildingBlocks;

namespace PriceNegotiationApp.Modules.Identity.Features.Auth;

internal static class Me
{
    internal static RouteGroupBuilder MapMe(this RouteGroupBuilder group) =>
        group.MapGet("/me", (ClaimsPrincipal principal) =>
            {
                var caller = principal.ToCallerContext();
                return TypedResults.Ok(new CurrentUserResponse(caller.UserId, caller.Email, caller.Roles.ToList()));
            })
        .RequireAuthorization();
}
```

**Step 7 — Module composition**

`IdentityModule.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Modules.Identity.Auth;
using PriceNegotiationApp.Modules.Identity.Features.Auth;
using PriceNegotiationApp.Modules.Identity.Persistence;
using PriceNegotiationApp.Modules.Identity.Seeding;

namespace PriceNegotiationApp.Modules.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdentityModuleDbContext>(options => options
            .UseNpgsql(DbConnections.Resolve(configuration, "Identity"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Identity"))
            .UseSnakeCaseNamingConvention());

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<IdentityModuleDbContext>()
            .AddDefaultTokenProviders();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<JwtManager>();

        services.AddOptions<SeedingOptions>()
            .Bind(configuration.GetSection(SeedingOptions.SectionName));
        services.AddHostedService<IdentitySeedingHostedService>();

        return services;
    }

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");
        group.MapRegister();
        group.MapLogin();
        group.MapMe();
        return app;
    }
}
```

(The duplicate `AddSingleton(TimeProvider.System)` across modules is harmless — identical implementation, last registration wins.)

**Step 8 — Host wiring + legacy deletion**

1. `WebApplicationBuilderExtensions.AddApiServices`: add `builder.Services.AddIdentityModule(configuration);`. Remove the `AddApplicationServices()` / `AddInfrastructure(configuration)` calls.
2. Delete the legacy projects entirely (they are empty shells now):

```powershell
git rm -r src/PriceNegotiationApp.Application src/PriceNegotiationApp.Domain src/PriceNegotiationApp.Infrastructure
```

3. Api `.csproj`: delete the `<ProjectReference>` lines for Application and Infrastructure.
4. `PipelineExtensions.MapModules`: `app.MapAuthApi();` → `app.MapAuthEndpoints();`.
5. `Composition/MigrationHostedService.cs` using swap: `PriceNegotiationApp.Infrastructure.Persistence` → `PriceNegotiationApp.Modules.Identity.Persistence`.
6. `GlobalExceptionHandler.cs` final usings:

```csharp
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.Modules.Negotiations.Features;
using Vogen;
```

Switch arms unchanged except sources: the two negotiation exceptions resolve from Negotiations domain; code values come from `NegotiationErrorCodes.ProposalExceedsLimit` / `.NegotiationClosed`.

7. Health checks stay three `AddDbContextCheck<T>` calls with module context types.
8. JWT inbound validation stays exactly as-is (`JwtSettings` binds the same `"Jwt"` section as the module's `JwtOptions` — issuance and validation read one configuration from different assemblies, by design).
9. Remove the deleted projects' slnx entries.

**Step 9 — Identity unit tests**

`tests/PriceNegotiationApp.Modules.Identity.Tests/PriceNegotiationApp.Modules.Identity.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <NoWarn>$(NoWarn);CA1707;S1118</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\PriceNegotiationApp.Modules.Identity\PriceNegotiationApp.Modules.Identity.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Shouldly" />
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>
</Project>
```

Move `JwtManagerShould.cs` (namespace `PriceNegotiationApp.Modules.Identity.Tests`; using swap to `PriceNegotiationApp.Modules.Identity.Auth`). If `UnitTests` is now empty, delete the project and its slnx entry.

**Step 10 — Validate**

```powershell
dotnet build PriceNegotiationApp.slnx
dotnet test tests/PriceNegotiationApp.Modules.Identity.Tests
dotnet test tests/PriceNegotiationApp.IntegrationTests
```

Full suite green: auth flows (register/login/me/lockout/duplicates), products, negotiations — byte-identical over HTTP.

**Step 11 — Commit**

```bash
git add -A && git commit -m "refactor(modules): carve out Identity module; delete legacy layered projects"
```


---

### Task 7: Rename Api → AppHost; update solution, Docker, CI

**Files:**
- Rename: `src/PriceNegotiationApp.Api/` → `src/PriceNegotiationApp.AppHost/` (+ csproj filename)
- Modify: every file under that folder with namespace `PriceNegotiationApp.Api` (≈10 files), `PriceNegotiationApp.slnx`, `Dockerfile`, `.github/workflows/ci.yml`

**Interfaces:**
- Produces:
  - Root namespace `PriceNegotiationApp.AppHost`; project `PriceNegotiationApp.AppHost.csproj`
  - OTel service name `PriceNegotiationApp.AppHost`
  - Runtime entrypoint dll `PriceNegotiationApp.AppHost.dll`

- [ ] **Step 1: Rename folder + project**

```powershell
Rename-Item src/PriceNegotiationApp.Api PriceNegotiationApp.AppHost
Rename-Item src/PriceNegotiationApp.AppHost/PriceNegotiationApp.Api.csproj PriceNegotiationApp.AppHost.csproj
```

- [ ] **Step 2: Namespace sweep**

Global replace `namespace PriceNegotiationApp.Api` → `namespace PriceNegotiationApp.AppHost`, plus all `using PriceNegotiationApp.Api...` occurrences anywhere in the repo (integration tests do not use them — they go through HTTP only). Files affected: `Program.cs`, `GlobalExceptionHandler.cs`, `Extensions/*.cs` (4), `Composition/*.cs` (2–3).

In `WebApplicationBuilderExtensions`, update the OTel resource:

```csharp
.ConfigureResource(resource => resource.AddService("PriceNegotiationApp.AppHost"))
```

- [ ] **Step 3: slnx**

The `/src/` folder must list exactly:

```xml
<Project Path="src/PriceNegotiationApp.AppHost/PriceNegotiationApp.AppHost.csproj" />
<Project Path="src/PriceNegotiationApp.BuildingBlocks/PriceNegotiationApp.BuildingBlocks.csproj" />
<Project Path="src/PriceNegotiationApp.Modules.Catalog/PriceNegotiationApp.Modules.Catalog.csproj" />
<Project Path="src/PriceNegotiationApp.Modules.Identity/PriceNegotiationApp.Modules.Identity.csproj" />
<Project Path="src/PriceNegotiationApp.Modules.Negotiations/PriceNegotiationApp.Modules.Negotiations.csproj" />
```

and `/tests/` lists the integration project plus whichever module test projects survived Task 6 (Identity/Catalog/Negotiations Tests).

- [ ] **Step 4: Dockerfile**

Update build/publish paths and entrypoint (rest of file unchanged):

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src ./src
RUN dotnet restore src/PriceNegotiationApp.AppHost/PriceNegotiationApp.AppHost.csproj
RUN dotnet publish src/PriceNegotiationApp.AppHost/PriceNegotiationApp.AppHost.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
USER app
HEALTHCHECK --interval=30s --timeout=5s CMD ["/usr/bin/wget", "-qO-", "http://localhost:8080/health/live"]
ENTRYPOINT ["dotnet", "PriceNegotiationApp.AppHost.dll"]
```

- [ ] **Step 5: CI**

`.github/workflows/ci.yml`: keep restore/format/build; collapse testing into one solution-wide step:

```yaml
      - name: Test
        run: dotnet test PriceNegotiationApp.slnx -c Release --no-build --collect:"XPlat Code Coverage"
```

- [ ] **Step 6: Dependency-graph audit**

```powershell
Get-ChildItem -Recurse -Filter *.csproj | Where-Object FullName -notmatch '\\(bin|obj)\\' |
  Select-String -Pattern 'ProjectReference'
```

Expected: AppHost → the three modules (direct BuildingBlocks reference optional); each module → BuildingBlocks only; **no module references another module**. If a module-to-module edge appears, refactor it through a host adapter before proceeding.

- [ ] **Step 7: Validate**

```powershell
dotnet build PriceNegotiationApp.slnx
dotnet test PriceNegotiationApp.slnx
docker compose up --build -d ; Start-Sleep 20 ; curl http://localhost:8080/health/ready ; docker compose down
```

(Compose smoke optional locally when Docker is unavailable — CI covers it.)

- [ ] **Step 8: Commit**

```bash
git add -A && git commit -m "refactor(host): rename Api to AppHost, finalize solution graph and platform files"
```

---

### Task 8: Documentation, final sweep, definition-of-done

**Files:**
- Modify: `README.md`, `docs/sql/cleanup-legacy-tables.sql` (notes); review-only: `PriceNegotiationApp.http` (routes unchanged)

- [ ] **Step 1: README architecture section**

Replace the Architecture block with:

````markdown
## Architecture

Modular monolith: three bounded contexts behind compiler-enforced boundaries,
one PostgreSQL schema per context.

```
src/
  PriceNegotiationApp.AppHost               composition root: pipeline, authN/authZ,
  │                                         ProblemDetails, rate limiting, CORS, output
  │                                         caching, health checks, OTel; wires modules
  │                                         and the single inter-module adapter
  PriceNegotiationApp.BuildingBlocks        shared primitives (CallerContext, paging,
                                            error semantics, policy names)
  PriceNegotiationApp.Modules.Identity      users/roles/JWT issuance/seeding → schema identity
  PriceNegotiationApp.Modules.Catalog       products → schema catalog
  PriceNegotiationApp.Modules.Negotiations  negotiations/customers/policy → schema negotiations

tests/
  PriceNegotiationApp.Modules.*.Tests       per-module unit tests (public surface only)
  PriceNegotiationApp.IntegrationTests      WebApplicationFactory + Testcontainers PostgreSQL
```

Rules: modules never reference each other; cross-module interaction flows through
consumer-owned ports wired in AppHost (`Composition/CatalogToNegotiations` is currently
the only edge). Each context owns its migrations; startup applies them in order
identity → catalog → negotiations.
````

Add configuration row:

```markdown
| `Database:Modules:{Identity\|Catalog\|Negotiations}:ConnectionString` | optional per-module DB override; defaults to `Database:ConnectionString` |
```

Add negotiation rule:

```markdown
6. Deleting a product does not delete or block its negotiations — they keep their
   price snapshot (product existence is only validated when a negotiation is created).
```

Add migration cheat sheet:

````markdown
### Migrations

Each module owns its migration stream (history tables live in the default schema):

```bash
dotnet ef migrations add <Name> --context CatalogDbContext `
  -p src/PriceNegotiationApp.Modules.Catalog -o Persistence/Migrations
```
````

- [ ] **Step 2: Cleanup-script notes**

Ensure `docs/sql/cleanup-legacy-tables.sql` reads:

```sql
-- One-time maintenance after the first successful start of the new version
-- on an upgraded persistent database:
DELETE FROM "__EFMigrationsHistory_Identity";
DELETE FROM "__EFMigrationsHistory_Catalog";
DELETE FROM "__EFMigrationsHistory_Negotiations";
DROP TABLE IF EXISTS public.negotiations CASCADE;
DROP TABLE IF EXISTS public.customers CASCADE;
DROP TABLE IF EXISTS public.products CASCADE;
DROP TABLE IF EXISTS public.__efmigrations_history CASCADE;
```

- [ ] **Step 3: Final sweep**

```powershell
dotnet format --verify-no-changes
dotnet build PriceNegotiationApp.slnx -c Release
dotnet test PriceNegotiationApp.slnx -c Release
```

Fix anything flagged; commit fixes separately if non-trivial.

- [ ] **Step 4: Definition-of-done checklist (spec §11)**

- [ ] Each DbContext lives in exactly one module project.
- [ ] No module `.csproj` references another module.
- [ ] `dotnet ef migrations list --context X` shows an independent stream per context.
- [ ] Integration suite passes unchanged, plus the two additions from Task 2.
- [ ] No `IRepository`/`IUnitOfWork` symbols remain.
- [ ] `.env` untracked; stale folders gone; warnings-as-errors Release build green.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "docs: modular monolith architecture, config and migration guide"
```

---

## Execution Notes for Reviewers

- **Regression gate:** Tasks 2–6 each end with the full integration suite green. Any drift in status codes or payload shapes is a task failure — stop and reconcile against the spec's frozen-contract rule instead of editing tests to match drift.
- **Superseded blocks:** Task 4 contains two marked superseded snippets (`CreateNegotiationRequest`, first `FindOpenAsync`) — ship the second version of each pair. Same for Task 6 (`Me.cs`).
- **Parallelization:** Tasks 4, 5, 6 touch disjoint projects but share `PipelineExtensions` / `WebApplicationBuilderExtensions` / slnx edits — run them serially, or resolve those three shared files manually if parallelizing.
- **Test-count expectation:** unit-test count drops slightly (`*ServiceShould` files deleted with their subjects); coverage moves into the integration matrices where those branches are exercised over HTTP. This is intentional per spec §9.
