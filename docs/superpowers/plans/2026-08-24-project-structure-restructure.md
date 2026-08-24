# Project Structure Restructure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restructure the modular monolith per `docs/superpowers/specs/2026-08-24-project-structure-design.md`: rename projects, normalize module layouts, slim the shared kernel, de-duplicate plumbing, enforce compile-time ownership, and remove cruft.

**Architecture:** Modular monolith: `Api` host (composition root) → three feature modules → `SharedKernel` primitive library. Modules reference only SharedKernel; the one inter-module edge is `Ports/IProductPriceProvider` implemented by an Api adapter. After this restructure, module implementation types are `internal`; the composition root and each module's own test project get `InternalsVisibleTo` grants.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, EF Core + Npgsql (one DB per module), xunit.v3 + Shouldly, Testcontainers for integration tests, Central Package Management.

## Global Constraints

- Target framework `net10.0` everywhere (set centrally in `Directory.Build.props` — never set per-project).
- `TreatWarningsAsErrors=true`, analyzers enforced — every build must pass clean.
- Central Package Management via `Directory.Packages.props`: never put `Version=` on a `PackageReference`.
- **No API route, response-contract, database schema, migration, or feature behavior changes.**
- Only one new package reference allowed in this whole plan: `Microsoft.EntityFrameworkCore.Design` added to SharedKernel (already versioned in CPM).
- All shell commands are PowerShell 7 (`pwsh`). Run from repo root unless stated otherwise.
- Use `git mv` for moves/renames so history follows files.
- When bulk-replacing text in `.cs` files, ALWAYS exclude `bin`/`obj` directories.
- Test commands: unit test projects run without Docker; integration tests need a running Docker daemon (Testcontainers).

## File Structure (end state)

```
src/
├── PriceNegotiationApp.Api/                        (renamed from AppHost)
│   ├── Composition/{MigrationHostedService,CatalogToNegotiations}.cs   [modified]
│   ├── Extensions/{WebApplicationBuilderExtensions,PipelineExtensions,JwtSettings,RateLimitingOptions}.cs [modified]
│   ├── GlobalExceptionHandler.cs, Program.cs       [modified]
├── PriceNegotiationApp.SharedKernel/               (renamed from BuildingBlocks)
│   ├── CallerContext.cs, CallerContextExtensions.cs, DbConnections.cs, EndpointConventionExtensions.cs,
│   │   ErrorCodes.cs, Exceptions.cs, PagedResult.cs, PageQuery.cs, Policies.cs, UserRoles.cs  [renamed ns only]
│   ├── ModuleSeedingHostedServiceBase.cs           [new]
│   ├── DesignTimeDbContextFactoryBase.cs           [new]
│   ├── ProductQuery.cs                             [deleted — moved to Catalog]
├── PriceNegotiationApp.Modules.Catalog/
│   ├── CatalogModule.cs, CatalogEndpoints.cs       [modified]
│   ├── Features/Products/{Create,Get,List,Update,Delete,ProductModels}.cs [modified]
│   ├── Features/Products/ProductQuery.cs           [moved from SharedKernel]
│   ├── Persistence/…                               [DesignTimeDbContextFactory rewritten]
│   ├── Seeding/CatalogSeedingHostedService.cs      [rewritten onto base]
├── PriceNegotiationApp.Modules.Identity/
│   ├── IdentityModule.cs, IdentityEndpoints.cs     [modified]
│   ├── Features/Auth/{Login,Register,Me,AuthModels,JwtManager,JwtOptions,JwtOptionsValidator}.cs
│   │                                               (Jwt* moved from Auth/ folder)
│   ├── Persistence/…, Public/IdentityErrorCodes.cs [factory rewritten; Public unchanged]
│   └── Seeding/{IdentitySeedingHostedService,SeedingOptions}.cs [rewritten / trimmed]
├── PriceNegotiationApp.Modules.Negotiations/
│   ├── NegotiationsModule.cs, NegotiationEndpoints.cs [modified]
│   ├── Domain/…                                    [ns changes only]
│   ├── Features/Negotiations/{Accept,CounterPropose,Create,Decline,Get,List,ListMine,Withdraw,NegotiationAccess,NegotiationModels}.cs
│   │                                               (moved from flat Features/)
│   ├── Ports/IProductPriceProvider.cs              [unchanged, stays public]
│   └── Persistence/…                               [factory rewritten]
tests/
├── PriceNegotiationApp.IntegrationTests/           [csproj ref path updated]
├── PriceNegotiationApp.Modules.{Catalog|Identity|Negotiations}.Tests [usings updated]
(root) Directory.Packages.props, Dockerfile, PriceNegotiationApp.slnx, README.md [modified]
```

---

### Task 1: Rename `AppHost` project to `Api`

**Files:**
- Rename: `src/PriceNegotiationApp.AppHost/` → `src/PriceNegotiationApp.Api/` (folder + csproj)
- Modify: `PriceNegotiationApp.slnx`, `Dockerfile`, `tests/PriceNegotiationApp.IntegrationTests/PriceNegotiationApp.IntegrationTests.csproj`
- Modify: all `.cs` files containing namespace `PriceNegotiationApp.AppHost` (Program.cs, Extensions/*, Composition/*, GlobalExceptionHandler.cs)

**Interfaces:**
- Produces: root namespace/assembly `PriceNegotiationApp.Api`; solution entry `src/PriceNegotiationApp.Api/PriceNegotiationApp.Api.csproj`. Later tasks and the final verification rely on this exact name.

- [ ] **Step 1: Move folder and rename csproj**

```bash
git mv src/PriceNegotiationApp.AppHost src/PriceNegotiationApp.Api
git mv src/PriceNegotiationApp.Api/PriceNegotiationApp.AppHost.csproj src/PriceNegotiationApp.Api/PriceNegotiationApp.Api.csproj
```

- [ ] **Step 2: Replace namespace and string literals in source**

The literal `"PriceNegotiationApp.AppHost"` also appears as the OpenTelemetry service name in `WebApplicationBuilderExtensions.cs`; a plain text replace fixes both.

```powershell
Get-ChildItem src,tests -Recurse -Filter *.cs |
  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
  ForEach-Object {
    $c = Get-Content $_.FullName -Raw
    if ($c -match 'PriceNegotiationApp\.AppHost') {
      Set-Content $_.FullName ($c -replace 'PriceNegotiationApp\.AppHost', 'PriceNegotiationApp.Api') -NoNewline
    }
  }
```

- [ ] **Step 3: Update solution, Dockerfile, and test project reference**

In `PriceNegotiationApp.slnx`, change line:
```xml
<Project Path="src/PriceNegotiationApp.AppHost/PriceNegotiationApp.AppHost.csproj" />
```
to:
```xml
<Project Path="src/PriceNegotiationApp.Api/PriceNegotiationApp.Api.csproj" />
```

In `Dockerfile`, change the two build/publish paths and the ENTRYPOINT:
```dockerfile
RUN dotnet restore src/PriceNegotiationApp.Api/PriceNegotiationApp.Api.csproj
RUN dotnet publish src/PriceNegotiationApp.Api/PriceNegotiationApp.Api.csproj -c Release -o /app --no-restore
ENTRYPOINT ["dotnet", "PriceNegotiationApp.Api.dll"]
```

In `tests/PriceNegotiationApp.IntegrationTests/PriceNegotiationApp.IntegrationTests.csproj`, change:
```xml
<ProjectReference Include="..\..\src\PriceNegotiationApp.Api\PriceNegotiationApp.Api.csproj" />
```

- [ ] **Step 4: Validate**

```bash
dotnet build PriceNegotiationApp.slnx
dotnet test tests/PriceNegotiationApp.IntegrationTests --no-build --filter "Category!=Skip"
```
(If Docker isn't available, note it and continue — Task 13 runs the full suite.)

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor: rename AppHost project to Api"
```

---

### Task 2: Rename `BuildingBlocks` project to `SharedKernel`

**Files:**
- Rename: `src/PriceNegotiationApp.BuildingBlocks/` → `src/PriceNegotiationApp.SharedKernel/` (folder + csproj)
- Modify: `PriceNegotiationApp.slnx`, all four src csproj `ProjectReference` entries, every `.cs` file with namespace/usings `PriceNegotiationApp.BuildingBlocks` (in src and tests)

**Interfaces:**
- Consumes: nothing from Task 1 except the updated solution.
- Produces: root namespace/assembly `PriceNegotiationApp.SharedKernel`. All later tasks use `using PriceNegotiationApp.SharedKernel;`.

- [ ] **Step 1: Move folder and rename csproj**

```bash
git mv src/PriceNegotiationApp.BuildingBlocks src/PriceNegotiationApp.SharedKernel
git mv src/PriceNegotiationApp.SharedKernel/PriceNegotiationApp.BuildingBlocks.csproj src/PriceNegotiationApp.SharedKernel/PriceNegotiationApp.SharedKernel.csproj
```

- [ ] **Step 2: Replace namespace in source**

```powershell
Get-ChildItem src,tests -Recurse -Filter *.cs |
  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
  ForEach-Object {
    $c = Get-Content $_.FullName -Raw
    if ($c -match 'PriceNegotiationApp\.BuildingBlocks') {
      Set-Content $_.FullName ($c -replace 'PriceNegotiationApp\.BuildingBlocks', 'PriceNegotiationApp.SharedKernel') -NoNewline
    }
  }
```

- [ ] **Step 3: Update csproj references and solution**

In `src/PriceNegotiationApp.Api/PriceNegotiationApp.Api.csproj`:
```xml
<ProjectReference Include="..\PriceNegotiationApp.SharedKernel\PriceNegotiationApp.SharedKernel.csproj" />
```
In each of `src/PriceNegotiationApp.Modules.{Catalog,Identity,Negotiations}/*.csproj`:
```xml
<ProjectReference Include="..\..\src\PriceNegotiationApp.SharedKernel\PriceNegotiationApp.SharedKernel.csproj" />
```
In `PriceNegotiationApp.slnx`:
```xml
<Project Path="src/PriceNegotiationApp.SharedKernel/PriceNegotiationApp.SharedKernel.csproj" />
```

- [ ] **Step 4: Validate**

```bash
dotnet build PriceNegotiationApp.slnx
```

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor: rename BuildingBlocks project to SharedKernel"
```

---

### Task 3: Move `ProductQuery` out of SharedKernel into Catalog

`ProductQuery` is Catalog's list-query DTO; the shared kernel must contain nothing module-specific.

**Files:**
- Move: `src/PriceNegotiationApp.SharedKernel/ProductQuery.cs` → `src/PriceNegotiationApp.Modules.Catalog/Features/Products/ProductQuery.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `PriceNegotiationApp.Modules.Catalog.Features.Products.ProductQuery` (record, same shape: `string? Search, decimal? MinPrice, decimal? MaxPrice, string? SortBy, bool SortDesc, int Page, int PageSize`). Same namespace as its only consumer (`List.cs`), which needs no using change.

- [ ] **Step 1: Move and re-namespace**

```bash
git mv src/PriceNegotiationApp.SharedKernel/ProductQuery.cs src/PriceNegotiationApp.Modules.Catalog/Features/Products/ProductQuery.cs
```

Edit `src/PriceNegotiationApp.Modules.Catalog/Features/Products/ProductQuery.cs` — replace the first line:
```csharp
namespace PriceNegotiationApp.SharedKernel;
```
with:
```csharp
namespace PriceNegotiationApp.Modules.Catalog.Features.Products;
```

- [ ] **Step 2: Validate**

```bash
dotnet build PriceNegotiationApp.slnx
dotnet test tests/PriceNegotiationApp.Modules.Catalog.Tests --no-build
```

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "refactor: move ProductQuery into Catalog module"
```

---

### Task 4: Add shared plumbing bases to SharedKernel

Two bases remove the triplicated hosted-service ceremony and design-time-factory boilerplate. The design-time base deliberately keeps provider configuration (`UseNpgsql`) in the modules so Npgsql never becomes a SharedKernel dependency.

**Files:**
- Create: `src/PriceNegotiationApp.SharedKernel/ModuleSeedingHostedServiceBase.cs`
- Create: `src/PriceNegotiationApp.SharedKernel/DesignTimeDbContextFactoryBase.cs`
- Modify: `src/PriceNegotiationApp.SharedKernel/PriceNegotiationApp.SharedKernel.csproj`

**Interfaces:**
- Produces (used verbatim by Tasks 5–6):
  - `abstract class ModuleSeedingHostedServiceBase(IServiceScopeFactory scopeFactory) : IHostedService` with `protected abstract Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken);`
  - `abstract class DesignTimeDbContextFactoryBase<TContext> : IDesignTimeDbContextFactory<TContext> where TContext : DbContext` with `protected const string LocalConnectionString`, `protected abstract void Configure(DbContextOptionsBuilder<TContext> builder);` and `protected abstract TContext Create(DbContextOptions<TContext> options);`

- [ ] **Step 1: Add EF Design package to SharedKernel csproj**

`IDesignTimeDbContextFactory<T>` lives in the `Microsoft.EntityFrameworkCore.Design` package (dev-time only, already versioned centrally). Add this ItemGroup to `src/PriceNegotiationApp.SharedKernel/PriceNegotiationApp.SharedKernel.csproj` (the csproj currently has only the FrameworkReference item group):
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  </PackageReference>
</ItemGroup>
```

- [ ] **Step 2: Create the seeding base**

Create `src/PriceNegotiationApp.SharedKernel/ModuleSeedingHostedServiceBase.cs`:
```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace PriceNegotiationApp.SharedKernel;

/// <summary>
/// Runs a module's seed routine once at host start inside a scope that is disposed afterwards.
/// </summary>
public abstract class ModuleSeedingHostedServiceBase(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await SeedAsync(scope.ServiceProvider, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Seed the module's data. Resolve services from <paramref name="services"/>.</summary>
    protected abstract Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken);
}
```

- [ ] **Step 3: Create the design-time factory base**

Create `src/PriceNegotiationApp.SharedKernel/DesignTimeDbContextFactoryBase.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PriceNegotiationApp.SharedKernel;

/// <summary>
/// Common plumbing for EF Core design-time factories. Provider configuration stays in each
/// module on purpose: Npgsql must not become a SharedKernel dependency.
/// </summary>
public abstract class DesignTimeDbContextFactoryBase<TContext> : IDesignTimeDbContextFactory<TContext>
    where TContext : DbContext
{
#pragma warning disable S2068 // Design-time default only; never used in production wiring.
    protected const string LocalConnectionString =
        "Host=localhost;Port=5432;Database=pricenego_design;Username=postgres;Password=postgres";
#pragma warning restore S2068

    public TContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        Configure(builder);
        return Create(builder.Options);
    }

    /// <summary>Apply provider options, e.g. UseNpgsql(LocalConnectionString, …) and naming conventions.</summary>
    protected abstract void Configure(DbContextOptionsBuilder<TContext> builder);

    /// <summary>Create the context instance, typically `new TContext(options)`.</summary>
    protected abstract TContext Create(DbContextOptions<TContext> options);
}
```

- [ ] **Step 4: Validate**

```bash
dotnet build PriceNegotiationApp.slnx
```

- [ ] **Step 5: Commit**

```bash
git add src/PriceNegotiationApp.SharedKernel
git commit -m "feat: add shared seeding and design-time factory bases to SharedKernel"
```

---

### Task 5: Rebuild module seeders on the shared base

Also removes a dead property (`SeedSampleProducts`) copied into Identity's `SeedingOptions` where no seeder reads it.

**Files:**
- Rewrite: `src/PriceNegotiationApp.Modules.Catalog/Seeding/CatalogSeedingHostedService.cs`
- Rewrite: `src/PriceNegotiationApp.Modules.Identity/Seeding/IdentitySeedingHostedService.cs`
- Modify: `src/PriceNegotiationApp.Modules.Identity/Seeding/SeedingOptions.cs` (remove dead property)

**Interfaces:**
- Consumes: `ModuleSeedingHostedServiceBase` exactly as defined in Task 4.

- [ ] **Step 1: Rewrite the Catalog seeder**

Replace the entire content of `src/PriceNegotiationApp.Modules.Catalog/Seeding/CatalogSeedingHostedService.cs` with:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceNegotiationApp.Modules.Catalog.Domain;
using PriceNegotiationApp.Modules.Catalog.Persistence;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Catalog.Seeding;

public sealed class CatalogSeedingHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<CatalogSeedingOptions> options,
    ILogger<CatalogSeedingHostedService> logger) : ModuleSeedingHostedServiceBase(scopeFactory)
{
    protected override async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        if (!options.Value.SeedSampleProducts)
        {
            return;
        }

        var db = services.GetRequiredService<CatalogDbContext>();
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
}
```

- [ ] **Step 2: Rewrite the Identity seeder**

Replace the entire content of `src/PriceNegotiationApp.Modules.Identity/Seeding/IdentitySeedingHostedService.cs` with:
```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceNegotiationApp.Modules.Identity.Persistence;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Identity.Seeding;

public sealed class IdentitySeedingHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<SeedingOptions> options,
    ILogger<IdentitySeedingHostedService> logger) : ModuleSeedingHostedServiceBase(scopeFactory)
{
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
`UserRoles` resolves via the `using PriceNegotiationApp.SharedKernel;` — it stays in the shared kernel per spec.

- [ ] **Step 3: Remove the dead property from Identity's SeedingOptions**

In `src/PriceNegotiationApp.Modules.Identity/Seeding/SeedingOptions.cs`, delete the line:
```csharp
    public bool SeedSampleProducts { get; init; }
```
(No Identity code reads it; `Seeding:SeedSampleProducts` in appsettings/config binds harmlessly nowhere for Identity.)

- [ ] **Step 4: Validate**

```bash
dotnet build PriceNegotiationApp.slnx
dotnet test tests/PriceNegotiationApp.Modules.Identity.Tests --no-build
```

- [ ] **Step 5: Commit**

```bash
git add src/PriceNegotiationApp.Modules.Catalog/Seeding src/PriceNegotiationApp.Modules.Identity/Seeding
git commit -m "refactor: rebuild module seeders on shared seeding base"
```

---

### Task 6: Rebuild the three design-time DbContext factories on the shared base

**Files:**
- Rewrite: `src/PriceNegotiationApp.Modules.Catalog/Persistence/DesignTimeDbContextFactory.cs`
- Rewrite: `src/PriceNegotiationApp.Modules.Identity/Persistence/DesignTimeDbContextFactory.cs`
- Rewrite: `src/PriceNegotiationApp.Modules.Negotiations/Persistence/DesignTimeDbContextFactory.cs`

**Interfaces:**
- Consumes: `DesignTimeDbContextFactoryBase<TContext>` exactly as defined in Task 4 (including `protected const string LocalConnectionString`).
- Produces: unchanged public class names `DesignTimeDbContextFactory` per module (EF tooling finds them by convention).

- [ ] **Step 1: Catalog factory**

Replace the entire content of `src/PriceNegotiationApp.Modules.Catalog/Persistence/DesignTimeDbContextFactory.cs` with:
```csharp
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Catalog.Persistence;

public sealed class DesignTimeDbContextFactory : DesignTimeDbContextFactoryBase<CatalogDbContext>
{
    protected override void Configure(DbContextOptionsBuilder<CatalogDbContext> builder) =>
        builder.UseNpgsql(LocalConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Catalog"))
            .UseSnakeCaseNamingConvention();

    protected override CatalogDbContext Create(DbContextOptions<CatalogDbContext> options) => new(options);
}
```

- [ ] **Step 2: Identity factory**

Replace the entire content of `src/PriceNegotiationApp.Modules.Identity/Persistence/DesignTimeDbContextFactory.cs` with:
```csharp
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Identity.Persistence;

public sealed class DesignTimeDbContextFactory : DesignTimeDbContextFactoryBase<IdentityModuleDbContext>
{
    protected override void Configure(DbContextOptionsBuilder<IdentityModuleDbContext> builder) =>
        builder.UseNpgsql(LocalConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Identity"))
            .UseSnakeCaseNamingConvention();

    protected override IdentityModuleDbContext Create(DbContextOptions<IdentityModuleDbContext> options) => new(options);
}
```

- [ ] **Step 3: Negotiations factory**

Replace the entire content of `src/PriceNegotiationApp.Modules.Negotiations/Persistence/DesignTimeDbContextFactory.cs` with:
```csharp
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Negotiations.Persistence;

public sealed class DesignTimeDbContextFactory : DesignTimeDbContextFactoryBase<NegotiationsDbContext>
{
    protected override void Configure(DbContextOptionsBuilder<NegotiationsDbContext> builder) =>
        builder.UseNpgsql(LocalConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Negotiations"))
            .UseSnakeCaseNamingConvention();

    protected override NegotiationsDbContext Create(DbContextOptions<NegotiationsDbContext> options) => new(options);
}
```

- [ ] **Step 4: Validate migrations still resolve their factories**

```bash
dotnet build PriceNegotiationApp.slnx
dotnet ef migrations list --project src/PriceNegotiationApp.Modules.Catalog --no-build
dotnet ef migrations list --project src/PriceNegotiationApp.Modules.Identity --no-build
dotnet ef migrations list --project src/PriceNegotiationApp.Modules.Negotiations --no-build
```
(`dotnet-ef` must be installed; if unavailable, `dotnet build` passing is acceptable evidence — the factories are exercised again by integration tests.)

- [ ] **Step 5: Commit**

```bash
git add src/PriceNegotiationApp.Modules.Catalog/Persistence src/PriceNegotiationApp.Modules.Identity/Persistence src/PriceNegotiationApp.Modules.Negotiations/Persistence
git commit -m "refactor: dedupe design-time DbContext factories onto shared base"
```

---

### Task 7: Nest Negotiations features under `Features/Negotiations/`

Makes the module layout match Catalog (`Features/<Entity>/`). Pure move + namespace change.

**Files:**
- Move: all ten files currently at `src/PriceNegotiationApp.Modules.Negotiations/Features/*.cs` → `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/`
- Modify: those files' `namespace`; `using` updates in `NegotiationEndpoints.cs` and `src/PriceNegotiationApp.Api/GlobalExceptionHandler.cs`

**Interfaces:**
- Produces: namespace `PriceNegotiationApp.Modules.Negotiations.Features.Negotiations` for Accept, CounterPropose, Create, Decline, Get, List, ListMine, Withdraw, NegotiationAccess, NegotiationModels.

- [ ] **Step 1: Move the files**

```bash
New-Item src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations -ItemType Directory | Out-Null
git mv src/PriceNegotiationApp.Modules.Negotiations/Features/Accept.cs `
       src/PriceNegotiationApp.Modules.Negotiations/Features/CounterPropose.cs `
       src/PriceNegotiationApp.Modules.Negotiations/Features/Create.cs `
       src/PriceNegotiationApp.Modules.Negotiations/Features/Decline.cs `
       src/PriceNegotiationApp.Modules.Negotiations/Features/Get.cs `
       src/PriceNegotiationApp.Modules.Negotiations/Features/List.cs `
       src/PriceNegotiationApp.Modules.Negotiations/Features/ListMine.cs `
       src/PriceNegotiationApp.Modules.Negotiations/Features/Withdraw.cs `
       src/PriceNegotiationApp.Modules.Negotiations/Features/NegotiationAccess.cs `
       src/PriceNegotiationApp.Modules.Negotiations/Features/NegotiationModels.cs `
       src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/
```

- [ ] **Step 2: Update namespaces and usings repo-wide**

Exact-string replace `PriceNegotiationApp.Modules.Negotiations.Features` → `PriceNegotiationApp.Modules.Negotiations.Features.Negotiations` in all `.cs` files. This simultaneously fixes the `namespace …;` declarations and every `using …;` (e.g., in `NegotiationEndpoints.cs` and `GlobalExceptionHandler.cs`). Files in `Features/Negotiations/` that previously had no using (same namespace) need none added.

```powershell
Get-ChildItem src,tests -Recurse -Filter *.cs |
  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
  ForEach-Object {
    $c = Get-Content $_.FullName -Raw
    if ($c -match 'PriceNegotiationApp\.Modules\.Negotiations\.Features') {
      Set-Content $_.FullName ($c -replace 'PriceNegotiationApp\.Modules\.Negotiations\.Features', 'PriceNegotiationApp.Modules.Negotiations.Features.Negotiations') -NoNewline
    }
  }
```

- [ ] **Step 3: Validate**

```bash
dotnet build PriceNegotiationApp.slnx
dotnet test tests/PriceNegotiationApp.Modules.Negotiations.Tests --no-build
```

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "refactor: nest Negotiations features under Features/Negotiations"
```

---

### Task 8: Fold Identity's `Auth/` folder into `Features/Auth/`

One feature, one folder: the JWT plumbing belongs next to Login/Register/Me.

**Files:**
- Move: `src/PriceNegotiationApp.Modules.Identity/Auth/{JwtManager,JwtOptions,JwtOptionsValidator}.cs` → `src/PriceNegotiationApp.Modules.Identity/Features/Auth/`
- Modify: those files' `namespace`; `using` updates in `IdentityModule.cs` and `tests/PriceNegotiationApp.Modules.Identity.Tests/*` (JwtManagerShould)

**Interfaces:**
- Produces: `PriceNegotiationApp.Modules.Identity.Features.Auth.JwtManager`, `.JwtOptions`, `.JwtOptionsValidator` (names/types unchanged).

- [ ] **Step 1: Move the files and remove the empty folder**

```bash
git mv src/PriceNegotiationApp.Modules.Identity/Auth/JwtManager.cs `
       src/PriceNegotiationApp.Modules.Identity/Auth/JwtOptions.cs `
       src/PriceNegotiationApp.Modules.Identity/Auth/JwtOptionsValidator.cs `
       src/PriceNegotiationApp.Modules.Identity/Features/Auth/
```

- [ ] **Step 2: Update namespaces and usings repo-wide**

```powershell
Get-ChildItem src,tests -Recurse -Filter *.cs |
  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
  ForEach-Object {
    $c = Get-Content $_.FullName -Raw
    if ($c -match 'PriceNegotiationApp\.Modules\.Identity\.Auth') {
      Set-Content $_.FullName ($c -replace 'PriceNegotiationApp\.Modules\.Identity\.Auth', 'PriceNegotiationApp.Modules.Identity.Features.Auth') -NoNewline
    }
  }
```

- [ ] **Step 3: Validate**

```bash
dotnet build PriceNegotiationApp.slnx
dotnet test tests/PriceNegotiationApp.Modules.Identity.Tests --no-build
```

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "refactor: fold Identity JWT auth plumbing into Features/Auth"
```

---

### Task 9: Enforce compile-time ownership — flip module internals to `internal`

Do this per module (three passes), building after each. Types in `Public/`, `Ports/`, and the root `XModule`/`XEndpoints` files stay `public`. Generated `Persistence/Migrations/*.Designer.cs` files are left untouched (generated code).

**Files (Catalog pass):**
- Modify: every hand-written `.cs` under `src/PriceNegotiationApp.Modules.Catalog/{Domain,Features,Persistence,Seeding}/` (excluding `Persistence/Migrations/*`)
- Modify: `src/PriceNegotiationApp.Modules.Catalog/PriceNegotiationApp.Modules.Catalog.csproj`

**Interfaces:**
- Consumes: nothing new.
- Produces: `<InternalsVisibleTo>` grants consumed by Api (already compiled against these types) and the Catalog test project.

- [ ] **Step 1: Flip visibility declarations in Catalog**

```powershell
$targets = Get-ChildItem src/PriceNegotiationApp.Modules.Catalog -Recurse -Filter *.cs |
  Where-Object { $_.FullName -notmatch '\\(bin|obj|Migrations)\\' -and $_.Name -notin @('CatalogModule.cs','CatalogEndpoints.cs') }
foreach ($f in $targets) {
  $c = Get-Content $f.FullName -Raw
  $n = [regex]::Replace($c, '\bpublic\s+(?=(?:sealed\s+|static\s+|abstract\s+|partial\s+|readonly\s+)*(?:class|record|interface|struct|enum)\b)', 'internal ')
  if ($n -ne $c) { Set-Content $f.FullName $n -NoNewline }
}
```

- [ ] **Step 2: Grant internals access in Catalog csproj**

Add to `src/PriceNegotiationApp.Modules.Catalog/PriceNegotiationApp.Modules.Catalog.csproj`:
```xml
<ItemGroup>
  <InternalsVisibleTo Include="PriceNegotiationApp.Api" />
  <InternalsVisibleTo Include="PriceNegotiationApp.Modules.Catalog.Tests" />
</ItemGroup>
```

- [ ] **Step 3: Validate Catalog**

```bash
dotnet build PriceNegotiationApp.slnx
dotnet test tests/PriceNegotiationApp.Modules.Catalog.Tests --no-build
```
Expected failure mode to watch for: a public member exposing an internal type. If the build errors point at `XModule`/`XEndpoints` signatures, widen ONLY the smallest type involved (do not blanket-revert).

- [ ] **Step 4: Commit**

```bash
git add src/PriceNegotiationApp.Modules.Catalog
git commit -m "refactor: make Catalog module implementation internal"
```

---

### Task 10: Enforce ownership — Identity pass

Same procedure for Identity. `Public/IdentityErrorCodes.cs` stays public.

**Files:**
- Modify: every hand-written `.cs` under `src/PriceNegotiationApp.Modules.Identity/{Features,Persistence,Seeding}/` (excluding `Persistence/Migrations/*`)
- Modify: `src/PriceNegotiationApp.Modules.Identity/PriceNegotiationApp.Modules.Identity.csproj`

- [ ] **Step 1: Flip visibility declarations in Identity**

```powershell
$targets = Get-ChildItem src/PriceNegotiationApp.Modules.Identity -Recurse -Filter *.cs |
  Where-Object { $_.FullName -notmatch '\\(bin|obj|Migrations|\\Public\\)' -and $_.Name -notin @('IdentityModule.cs','IdentityEndpoints.cs') }
foreach ($f in $targets) {
  $c = Get-Content $f.FullName -Raw
  $n = [regex]::Replace($c, '\bpublic\s+(?=(?:sealed\s+|static\s+|abstract\s+|partial\s+|readonly\s+)*(?:class|record|interface|struct|enum)\b)', 'internal ')
  if ($n -ne $c) { Set-Content $f.FullName $n -NoNewline }
}
```

- [ ] **Step 2: Grant internals access in Identity csproj**

Add to `src/PriceNegotiationApp.Modules.Identity/PriceNegotiationApp.Modules.Identity.csproj`:
```xml
<ItemGroup>
  <InternalsVisibleTo Include="PriceNegotiationApp.Api" />
  <InternalsVisibleTo Include="PriceNegotiationApp.Modules.Identity.Tests" />
</ItemGroup>
```

- [ ] **Step 3: Validate Identity**

```bash
dotnet build PriceNegotiationApp.slnx
dotnet test tests/PriceNegotiationApp.Modules.Identity.Tests --no-build
```

- [ ] **Step 4: Commit**

```bash
git add src/PriceNegotiationApp.Modules.Identity
git commit -m "refactor: make Identity module implementation internal"
```

---

### Task 11: Enforce ownership — Negotiations pass

`Ports/IProductPriceProvider.cs` stays public (host implements it). Domain exceptions and error codes stay in `Domain/` — they become visible to Api through its `InternalsVisibleTo` grant, keeping cohesion without making them public contracts.

**Files:**
- Modify: every hand-written `.cs` under `src/PriceNegotiationApp.Modules.Negotiations/{Domain,Features,Persistence}/` (excluding `Persistence/Migrations/*`)
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/PriceNegotiationApp.Modules.Negotiations.csproj`

- [ ] **Step 1: Flip visibility declarations in Negotiations**

```powershell
$targets = Get-ChildItem src/PriceNegotiationApp.Modules.Negotiations -Recurse -Filter *.cs |
  Where-Object { $_.FullName -notmatch '\\(bin|obj|Migrations|\\Ports\\)' -and $_.Name -notin @('NegotiationsModule.cs','NegotiationEndpoints.cs') }
foreach ($f in $targets) {
  $c = Get-Content $f.FullName -Raw
  $n = [regex]::Replace($c, '\bpublic\s+(?=(?:sealed\s+|static\s+|abstract\s+|partial\s+|readonly\s+)*(?:class|record|interface|struct|enum)\b)', 'internal ')
  if ($n -ne $c) { Set-Content $f.FullName $n -NoNewline }
}
```

- [ ] **Step 2: Grant internals access in Negotiations csproj**

Add to `src/PriceNegotiationApp.Modules.Negotiations/PriceNegotiationApp.Modules.Negotiations.csproj`:
```xml
<ItemGroup>
  <InternalsVisibleTo Include="PriceNegotiationApp.Api" />
  <InternalsVisibleTo Include="PriceNegotiationApp.Modules.Negotiations.Tests" />
</ItemGroup>
```

- [ ] **Step 3: Validate Negotiations + full solution**

```bash
dotnet build PriceNegotiationApp.slnx
dotnet test tests/PriceNegotiationApp.Modules.Negotiations.Tests --no-build
```

- [ ] **Step 4: Commit**

```bash
git add src/PriceNegotiationApp.Modules.Negotiations
git commit -m "refactor: make Negotiations module implementation internal"
```

---

### Task 12: Cruft removal and hygiene

**Files:**
- Delete: `tests/PriceNegotiationApp.UnitTests/` (untracked local residue: empty folders + stale bin/obj, no csproj)
- Delete: `src/PriceNegotiationApp.Api/PriceNegotiationApp.Api.csproj.user` (local state; may be named `*.AppHost.csproj.user`)
- Modify: `Directory.Packages.props` (remove NSubstitute line + empty transitive-overrides block)
- Verify: `PriceNegotiationApp.http`
- Modify: `README.md` (architecture section refresh)

- [ ] **Step 1: Delete ghost artifacts**

```powershell
Remove-Item -Recurse -Force tests/PriceNegotiationApp.UnitTests
Remove-Item -Force -ErrorAction SilentlyContinue src/PriceNegotiationApp.Api/*.csproj.user
```

- [ ] **Step 2: Clean Directory.Packages.props**

Delete the line:
```xml
    <PackageVersion Include="NSubstitute" Version="5.3.0" />
```
and the entire block:
```xml
  <!-- Transitive dependency overrides (security pins, flow to transitive graph
       because CentralPackageTransitivePinningEnabled is true) -->
  <ItemGroup Label="Transitive overrides">
  </ItemGroup>
```

- [ ] **Step 3: Verify the .http file**

Open `PriceNegotiationApp.http` and confirm `@host` matches launchSettings (`http://localhost:5185`). If it differs, set it to `http://localhost:5185`.

- [ ] **Step 4: Refresh README architecture section**

Update the README's architecture/project-structure text to reflect reality: `Api` host, `SharedKernel`, three modules, normalized layout (`Domain/ Features/<Entity>/ Persistence/ Ports/ Public/ Seeding/`), and the ownership rule (internals + `InternalsVisibleTo` for the composition root and tests only). Keep it short — a tree diagram plus 3–5 bullet points matching the spec's §1–§4. Do not invent new features.

- [ ] **Step 5: Validate**

```bash
dotnet build PriceNegotiationApp.slnx
```

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "chore: remove cruft and refresh docs after restructure"
```

---

### Task 13: Full verification and boundary audit

**Files:** none expected to change; fix-ups only if something surfaces.

- [ ] **Step 1: Clean full build (warnings-as-errors gate)**

```bash
dotnet clean PriceNegotiationApp.slnx && dotnet build PriceNegotiationApp.slnx
```

- [ ] **Step 2: Run every test suite**

```bash
dotnet test tests/PriceNegotiationApp.Modules.Catalog.Tests
dotnet test tests/PriceNegotiationApp.Modules.Identity.Tests
dotnet test tests/PriceNegotiationApp.Modules.Negotiations.Tests
dotnet test tests/PriceNegotiationApp.IntegrationTests
```
Integration tests require Docker (Testcontainers PostgreSQL). If Docker is unavailable, state that explicitly in the completion report rather than skipping silently.

- [ ] **Step 3: Boundary greps**

No module may reference another module's namespace anywhere:
```powershell
rg -l "using PriceNegotiationApp\.Modules\." src/PriceNegotiationApp.Modules.Catalog src/PriceNegotiationApp.Modules.Identity src/PriceNegotiationApp.Modules.Negotiations
```
Expected result: **no matches** (modules reference only `PriceNegotiationApp.SharedKernel` and their own namespaces).

Only the composition root reaches into module internals:
```powershell
rg -l "PriceNegotiationApp\.Modules\.[A-Za-z]+\.(Domain|Persistence|Features)" src/PriceNegotiationApp.Api
```
Expected matches: `Composition/CatalogToNegotiations.cs`, `Composition/MigrationHostedService.cs`, `Extensions/WebApplicationBuilderExtensions.cs` (health checks), `GlobalExceptionHandler.cs` — all sanctioned host privileges.

- [ ] **Step 4: Final commit if fix-ups were needed**

```bash
git status
git add -A
git commit -m "fix: boundary audit follow-ups"
```
(skip if working tree is clean)
