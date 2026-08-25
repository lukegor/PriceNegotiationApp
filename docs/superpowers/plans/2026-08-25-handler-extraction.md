# Handler Extraction Implementation Plan — Endpoints as Transport Adapters

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove `DbContext`/`UserManager` injection from all 15 route handlers by introducing per-operation `internal sealed` handler classes; endpoints become pure transport adapters.

**Architecture:** Each operation gets an injectable `internal sealed <Op>Handler` (primary constructor captures scoped dependencies) registered explicitly `AddScoped` in its module. Handlers own queries, mutation, `SaveChanges`, conflict translation and DTO construction, throwing the existing SharedKernel exceptions; endpoints keep routes, verbs, status shaping, auth/cache/rate-limit attributes and `ClaimsPrincipal → CallerContext` translation. Zero behavior change — the 37-test integration suite is the regression harness.

**Tech Stack:** ASP.NET Core minimal APIs (service-in-endpoint parameters), EF Core 10, xunit.v3/MTP.

## Global Constraints

- Source spec: `docs/superpowers/specs/2026-08-25-handler-extraction-design.md`.
- **Zero behavior change**: routes, verbs, status codes, ProblemDetails `code`s, cache/rate-limit policies, named-route metadata (`GetProductById`) must remain identical.
- No repositories/UoW abstractions; no MediatR; handlers are `internal sealed` and `AddScoped`.
- `ClaimsPrincipal` never enters a handler — handlers receive `CallerContext`.
- `Me` endpoint and health endpoints stay as-is.
- Every task: zero-warning build, touched integration suites green (Docker required), commit.
- Shell pwsh from repo root. Validation pattern that respects exit codes:

```powershell
dotnet build 2>&1 | Out-Null; if ($LASTEXITCODE -ne 0) { dotnet build 2>&1 | Select-String error | Select-Object -First 8 } else { <tests> }
```

---

### Task 1: Negotiations write-path handlers

**Files:**
- Create: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/CreateNegotiationHandler.cs`
- Create: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/CounterProposeHandler.cs`
- Create: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/AcceptHandler.cs`
- Create: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/RejectCurrentOfferHandler.cs`
- Create: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/WithdrawHandler.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Create.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/CounterPropose.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Accept.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/RejectCurrentOffer.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Withdraw.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/NegotiationsModule.cs`

**Interfaces:**
- Consumes: `NegotiationAccess` (`RequireAsync`, `RequireOwnedAsync`, `IsOwnerAsync`, `FindOpenAsync`, `GetOrCreateCustomerIdAsync`), `DbWriteGuard.SaveOrConflictAsync(this DbContext, Func<string,Exception>, CancellationToken)` (via `CreateNegotiationHandler`), response records in `NegotiationModels.cs`.
- Produces: handler contracts consumed by Tasks 2's read handlers' style and by the endpoints in this task:
  - `Task<NegotiationResponse> CreateNegotiationHandler.HandleAsync(CreateNegotiationRequest, CallerContext, CancellationToken)`
  - `Task<CounterProposalOutcome> CounterProposeHandler.HandleAsync(Guid id, CounterProposalRequest, CallerContext, CancellationToken)`
  - `Task<StaffActionResponse> AcceptHandler.HandleAsync(Guid id, CancellationToken)`
  - `Task<StaffActionResponse> RejectCurrentOfferHandler.HandleAsync(Guid id, CancellationToken)`
  - `Task WithdrawHandler.HandleAsync(Guid id, CallerContext, CancellationToken)`

- [ ] **Step 1: Create the five handlers**

`CreateNegotiationHandler.cs`:

```csharp
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.Modules.Negotiations.Persistence;
using PriceNegotiationApp.Modules.Negotiations.Ports;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal sealed class CreateNegotiationHandler(
    NegotiationsDbContext db,
    IProductPriceProvider products,
    INegotiationPolicy policy,
    TimeProvider clock)
{
    public async Task<NegotiationResponse> HandleAsync(
        CreateNegotiationRequest command, CallerContext caller, CancellationToken ct)
    {
        var snapshot = await products.GetAsync(command.ProductId, ct)
                       ?? throw new NotFoundException("Product", command.ProductId);

        if (await NegotiationAccess.FindOpenAsync(db, snapshot.ProductId, caller.UserId, ct) is not null)
        {
            throw new ConflictException(NegotiationErrorCodes.NegotiationAlreadyOpen,
                "An open negotiation already exists for this product.");
        }

        var customerId = await NegotiationAccess.GetOrCreateCustomerIdAsync(db, caller.UserId, ct);
        var negotiation = Negotiation.Start(customerId, snapshot.ProductId, snapshot.Price,
            command.ProposedPrice, clock.GetUtcNow(), policy);
        await db.Negotiations.AddAsync(negotiation, ct);

        // The partial unique index is the real guard; a race that slipped past the
        // pre-check above surfaces here as a 409 instead of a 500.
        await db.SaveOrConflictAsync(
            _ => new ConflictException(NegotiationErrorCodes.NegotiationAlreadyOpen,
                "An open negotiation already exists for this product."), ct);

        return NegotiationResponses.ToResponse(negotiation);
    }
}
```

(`NegotiationsDbContext` resolves via `using PriceNegotiationApp.Modules.Negotiations.Persistence;`
included in the usings above.)

`CounterProposeHandler.cs`:

```csharp
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal sealed class CounterProposeHandler(NegotiationsDbContext db, TimeProvider clock)
{
    public async Task<CounterProposalOutcome> HandleAsync(
        Guid id, CounterProposalRequest request, CallerContext caller, CancellationToken ct)
    {
        var negotiation = await NegotiationAccess.RequireOwnedAsync(db, caller, id, ct);

        var outcome = negotiation.CounterPropose(request.ProposedPrice, clock.GetUtcNow());
        if (outcome == NegotiationOutcome.NoProposalsRemaining)
        {
            throw new ConflictException(NegotiationErrorCodes.NoProposalsRemaining,
                "No proposals remain for this negotiation.");
        }

        await db.SaveChangesAsync(ct);
        return new CounterProposalOutcome(outcome.ToString(), NegotiationResponses.ToResponse(negotiation));
    }
}
```

`AcceptHandler.cs`:

```csharp
namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal sealed class AcceptHandler(NegotiationsDbContext db, TimeProvider clock)
{
    public async Task<StaffActionResponse> HandleAsync(Guid id, CancellationToken ct)
    {
        var negotiation = await NegotiationAccess.RequireAsync(db, id, ct);
        negotiation.Accept(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return new StaffActionResponse("accepted", NegotiationResponses.ToResponse(negotiation));
    }
}
```

`RejectCurrentOfferHandler.cs`:

```csharp
namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal sealed class RejectCurrentOfferHandler(NegotiationsDbContext db, TimeProvider clock)
{
    public async Task<StaffActionResponse> HandleAsync(Guid id, CancellationToken ct)
    {
        var negotiation = await NegotiationAccess.RequireAsync(db, id, ct);
        negotiation.RejectCurrentOffer(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return new StaffActionResponse("current_offer_rejected",
            NegotiationResponses.ToResponse(negotiation));
    }
}
```

`WithdrawHandler.cs`:

```csharp
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal sealed class WithdrawHandler(NegotiationsDbContext db, TimeProvider clock)
{
    public async Task HandleAsync(Guid id, CallerContext caller, CancellationToken ct)
    {
        var negotiation = await NegotiationAccess.RequireAsync(db, id, ct);

        if (caller.IsInRole(UserRoles.Admin))
        {
            db.Negotiations.Remove(negotiation);
        }
        else
        {
            if (!await NegotiationAccess.IsOwnerAsync(db, caller.UserId, negotiation, ct))
            {
                throw new ForbiddenAccessException();
            }

            negotiation.Withdraw(clock.GetUtcNow());
        }

        await db.SaveChangesAsync(ct);
    }
}
```

All five files share namespace `PriceNegotiationApp.Modules.Negotiations.Features.Negotiations`;
`AcceptHandler` / `RejectCurrentOfferHandler` need no extra usings beyond defaults.

- [ ] **Step 2: Slim the five endpoint files**

Each becomes transport-only (no EF/Persistence/Domain usings). Example — `Create.cs`:

```csharp
using PriceNegotiationApp.SharedKernel;
using System.Security.Claims;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal static class Create
{
    internal static void MapCreate(this RouteGroupBuilder group)
    {
        group.MapPost("/", async (CreateNegotiationRequest request, ClaimsPrincipal principal,
                CreateNegotiationHandler handler, CancellationToken ct) =>
            TypedResults.Created("/api/v1/negotiations/mine",
                await handler.HandleAsync(request, principal.ToCallerContext(), ct)))
        .RequireRoles(UserRoles.Customer);
    }
}
```

`CounterPropose.cs` body:

```csharp
        group.MapPatch("/{id:guid}/proposals", async (Guid id, CounterProposalRequest request,
                ClaimsPrincipal principal, CounterProposeHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(id, request, principal.ToCallerContext(), ct)))
        .RequireAuthorization();
```

`Accept.cs` body:

```csharp
        group.MapPost("/{id:guid}/accept", async (Guid id, AcceptHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(id, ct)))
        .RequireRoles(UserRoles.Admin, UserRoles.Staff);
```

`RejectCurrentOffer.cs` body (route stays `/decline`):

```csharp
        group.MapPost("/{id:guid}/decline", async (Guid id, RejectCurrentOfferHandler handler,
                CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(id, ct)))
        .RequireRoles(UserRoles.Admin, UserRoles.Staff);
```

`Withdraw.cs` body:

```csharp
        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal principal,
                WithdrawHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(id, principal.ToCallerContext(), ct);
            return TypedResults.NoContent();
        })
        .RequireAuthorization();
```

Remove now-unused usings from each (`Microsoft.EntityFrameworkCore`, `…Persistence`,
`…Domain`, `Microsoft.AspNetCore.Authorization` where `RequireAuthorization` moved off? —
keep whatever compiles clean; EnforceCodeStyleInBuild will flag stragglers).

- [ ] **Step 3: Register handlers**

In `NegotiationsModule.AddNegotiationsModule`, after the `INegotiationPolicy` registration
and before `return services;`:

```csharp
        services.AddScoped<CreateNegotiationHandler>();
        services.AddScoped<CounterProposeHandler>();
        services.AddScoped<AcceptHandler>();
        services.AddScoped<RejectCurrentOfferHandler>();
        services.AddScoped<WithdrawHandler>();
```

Add `using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;`.

- [ ] **Step 4: Validate**

```powershell
dotnet build 2>&1 | Out-Null; if ($LASTEXITCODE -eq 0) { dotnet test tests/PriceNegotiationApp.IntegrationTests --no-build 2>&1 | Select-String -Pattern 'failed:|succeeded:' | Select-Object -First 2 } else { dotnet build 2>&1 | Select-String error | Select-Object -First 8 }
```

37 integration tests green (Docker required).

- [ ] **Step 5: Commit**

```bash
git add src/PriceNegotiationApp.Modules.Negotiations
git commit -m "refactor(negotiations): write-path handlers own persistence, endpoints go transport-only"
```

---

### Task 2: Negotiations read/list handlers

**Files:**
- Create: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/GetNegotiationHandler.cs`
- Create: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/ListNegotiationsHandler.cs`
- Create: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/ListMyNegotiationsHandler.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Get.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/List.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/ListMine.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/NegotiationsModule.cs` (3 registrations)

**Interfaces:**
- Consumes Task 1 conventions; `NegotiationAccess.RequireReadOnlyAsync / CanAccessAsync / CustomerByIdentityAsync`.
- Produces:
  - `Task<NegotiationResponse> GetNegotiationHandler.HandleAsync(Guid id, CallerContext, CancellationToken)`
  - `Task<PagedResult<NegotiationResponse>> ListNegotiationsHandler.HandleAsync(PageQuery, CancellationToken)`
  - `Task<PagedResult<NegotiationResponse>> ListMyNegotiationsHandler.HandleAsync(PageQuery, CallerContext, CancellationToken)`

- [ ] **Step 1: Handlers**

`GetNegotiationHandler.cs`:

```csharp
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal sealed class GetNegotiationHandler(NegotiationsDbContext db)
{
    public async Task<NegotiationResponse> HandleAsync(Guid id, CallerContext caller, CancellationToken ct)
    {
        var negotiation = await NegotiationAccess.RequireReadOnlyAsync(db, id, ct);
        if (!await NegotiationAccess.CanAccessAsync(db, caller, negotiation, ct))
        {
            throw new ForbiddenAccessException();
        }

        return NegotiationResponses.ToResponse(negotiation);
    }
}
```

`ListNegotiationsHandler.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal sealed class ListNegotiationsHandler(NegotiationsDbContext db)
{
    public async Task<PagedResult<NegotiationResponse>> HandleAsync(PageQuery page, CancellationToken ct)
    {
        var q = db.Negotiations.AsNoTracking();
        var total = await q.LongCountAsync(ct);
        var items = await q.OrderByDescending(n => n.CreatedAtUtc)
            .Skip(page.Skip).Take(page.SafePageSize)
            .ToListAsync(ct);

        return new PagedResult<NegotiationResponse>(
            items.Select(NegotiationResponses.ToResponse).ToList(),
            page.SafePage, page.SafePageSize, total);
    }
}
```

`ListMyNegotiationsHandler.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal sealed class ListMyNegotiationsHandler(NegotiationsDbContext db)
{
    public async Task<PagedResult<NegotiationResponse>> HandleAsync(
        PageQuery page, CallerContext caller, CancellationToken ct)
    {
        var customer = await NegotiationAccess.CustomerByIdentityAsync(db, caller.UserId, ct);
        if (customer is null)
        {
            return new PagedResult<NegotiationResponse>([], page.SafePage, page.SafePageSize, 0);
        }

        var q = db.Negotiations.AsNoTracking().Where(n => n.CustomerId == customer.Id);
        var total = await q.LongCountAsync(ct);
        var items = await q.OrderByDescending(n => n.CreatedAtUtc)
            .Skip(page.Skip).Take(page.SafePageSize)
            .ToListAsync(ct);

        return new PagedResult<NegotiationResponse>(
            items.Select(NegotiationResponses.ToResponse).ToList(),
            page.SafePage, page.SafePageSize, total);
    }
}
```

- [ ] **Step 2: Slim the three endpoint files**

`Get.cs` (drop EF/Persistence usings; keep Authorization):

```csharp
using PriceNegotiationApp.SharedKernel;
using System.Security.Claims;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal static class Get
{
    internal static void MapGetOne(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal principal,
                GetNegotiationHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(id, principal.ToCallerContext(), ct)))
        .RequireAuthorization();
    }
}
```

`List.cs` body (namespace stays `…Modules.Negotiations.Features.Negotiations`):

```csharp
        group.MapGet("/", async (ListNegotiationsHandler handler, CancellationToken ct,
                int page = 1, int pageSize = 20) =>
            TypedResults.Ok(await handler.HandleAsync(new PageQuery(page, pageSize), ct)))
        .RequireRoles(UserRoles.Admin, UserRoles.Staff);
```

`ListMine.cs` body:

```csharp
        group.MapGet("/mine", async (ClaimsPrincipal principal, ListMyNegotiationsHandler handler,
                CancellationToken ct, int page = 1, int pageSize = 20) =>
            TypedResults.Ok(await handler.HandleAsync(
                new PageQuery(page, pageSize), principal.ToCallerContext(), ct)))
        .RequireRoles(UserRoles.Customer);
```

- [ ] **Step 3: Register**

In `NegotiationsModule`, alongside Task 1's registrations:

```csharp
        services.AddScoped<GetNegotiationHandler>();
        services.AddScoped<ListNegotiationsHandler>();
        services.AddScoped<ListMyNegotiationsHandler>();
```

- [ ] **Step 4: Validate + commit**

```powershell
dotnet build 2>&1 | Out-Null; if ($LASTEXITCODE -eq 0) { dotnet test tests/PriceNegotiationApp.IntegrationTests --no-build 2>&1 | Select-String -Pattern 'failed:|succeeded:' | Select-Object -First 2 } else { dotnet build 2>&1 | Select-String error | Select-Object -First 8 }
git add src/PriceNegotiationApp.Modules.Negotiations
git commit -m "refactor(negotiations): read and list handlers extracted"
```

---

### Task 3: Catalog handlers

**Files:**
- Create: `Features/Products/CreateProductHandler.cs`, `UpdateProductHandler.cs`, `DeleteProductHandler.cs`, `GetProductHandler.cs`, `ListProductsHandler.cs` (all under `src/PriceNegotiationApp.Modules.Catalog/Features/Products/`)
- Modify: the five Catalog endpoint files (`Create.cs`, `Update.cs`, `Delete.cs`, `Get.cs`, `List.cs`)
- Modify: `src/PriceNegotiationApp.Modules.Catalog/CatalogModule.cs` (5 registrations)

**Interfaces:**
- Produces:
  - `Task<ProductResponse> CreateProductHandler.HandleAsync(CreateProductRequest, CancellationToken)`
  - `Task<ProductResponse> UpdateProductHandler.HandleAsync(Guid id, UpdateProductRequest, CancellationToken)`
  - `Task DeleteProductHandler.HandleAsync(Guid id, CancellationToken)`
  - `Task<ProductResponse> GetProductHandler.HandleAsync(Guid id, CancellationToken)`
  - `Task<PagedResult<ProductResponse>> ListProductsHandler.HandleAsync(ProductQuery, CancellationToken)`

- [ ] **Step 1: Handlers**

`CreateProductHandler.cs`:

```csharp
using PriceNegotiationApp.Modules.Catalog.Domain;

namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

internal sealed class CreateProductHandler(CatalogDbContext db)
{
    public async Task<ProductResponse> HandleAsync(CreateProductRequest request, CancellationToken ct)
    {
        var product = Product.Create(request.Name, request.Price);
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return new ProductResponse(product.Id.Value, product.Name, product.Price);
    }
}
```

`UpdateProductHandler.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Modules.Catalog.Domain;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

internal sealed class UpdateProductHandler(CatalogDbContext db)
{
    public async Task<ProductResponse> HandleAsync(Guid id, UpdateProductRequest request, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == ProductId.From(id), ct)
                      ?? throw new NotFoundException("Product", id);

        product.Update(request.Name, request.Price);
        await db.SaveChangesAsync(ct);
        return new ProductResponse(product.Id.Value, product.Name, product.Price);
    }
}
```

`DeleteProductHandler.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Modules.Catalog.Domain;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

internal sealed class DeleteProductHandler(CatalogDbContext db)
{
    // Negotiations survive on their snapshots by design.
    public async Task HandleAsync(Guid id, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == ProductId.From(id), ct)
                      ?? throw new NotFoundException("Product", id);
        db.Products.Remove(product);
        await db.SaveChangesAsync(ct);
    }
}
```

`GetProductHandler.cs` (absorbs the old static projection query):

```csharp
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Modules.Catalog.Domain;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

internal sealed class GetProductHandler(CatalogDbContext db)
{
    public async Task<ProductResponse> HandleAsync(Guid id, CancellationToken ct) =>
        await db.Products.AsNoTracking()
            .Where(p => p.Id == ProductId.From(id))
            .Select(p => new ProductResponse(p.Id.Value, p.Name, p.Price))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Product", id);
}
```

`ListProductsHandler.cs` (absorbs `SearchAsync`; keeps `EF.Functions.ILike`, sort switch,
paging):

```csharp
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

internal sealed class ListProductsHandler(CatalogDbContext db)
{
    public async Task<PagedResult<ProductResponse>> HandleAsync(ProductQuery query, CancellationToken ct)
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

        q = (query.SortBy?.Trim().ToLowerInvariant(), query.SortDesc) switch
        {
            ("price", true) => q.OrderByDescending(p => p.Price),
            ("price", false) => q.OrderBy(p => p.Price),
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

- [ ] **Step 2: Slim endpoints**

Each Catalog endpoint file loses `Microsoft.EntityFrameworkCore` +
`…Catalog.Persistence` usings; lambdas resolve handlers instead of `db`.

`Create.cs`: `CreatedAtRoute("GetProductById", new { id = response.Id }, response)` where
`var response = await handler.HandleAsync(request, ct);`
`Update.cs`: `Ok(await handler.HandleAsync(id, request, ct))`.
`Delete.cs`: `await handler.HandleAsync(id, ct); return TypedResults.NoContent();` — keep
the existing snapshot-survival comment if present, relocated above the call.
`Get.cs`: `TypedResults.Ok(await handler.HandleAsync(id, ct))` keeping `.WithName`,
`.CacheOutput(Policies.ShortCachePolicy)`, `.AllowAnonymous()`; delete the now-empty
static `RequireAsync`.
`List.cs`: `TypedResults.Ok(await handler.HandleAsync(new ProductQuery(search, minPrice,
maxPrice, sortBy, sortDesc, page, pageSize), ct))` keeping cache attributes; delete static
`SearchAsync`.

- [ ] **Step 3: Register**

In `CatalogModule.AddCatalogModule` before `return services;`:

```csharp
        services.AddScoped<CreateProductHandler>();
        services.AddScoped<UpdateProductHandler>();
        services.AddScoped<DeleteProductHandler>();
        services.AddScoped<GetProductHandler>();
        services.AddScoped<ListProductsHandler>();
```

Add `using PriceNegotiationApp.Modules.Catalog.Features.Products;`.

- [ ] **Step 4: Validate + commit**

```powershell
dotnet build 2>&1 | Out-Null; if ($LASTEXITCODE -eq 0) { dotnet test tests/PriceNegotiationApp.IntegrationTests --no-build 2>&1 | Select-String -Pattern 'failed:|succeeded:' | Select-Object -First 2 } else { dotnet build 2>&1 | Select-String error | Select-Object -First 8 }
git add src/PriceNegotiationApp.Modules.Catalog
git commit -m "refactor(catalog): per-operation handlers own persistence"
```

---

### Task 4: Identity auth handlers

**Files:**
- Create: `src/PriceNegotiationApp.Modules.Identity/Features/Auth/RegisterUserHandler.cs`
- Create: `src/PriceNegotiationApp.Modules.Identity/Features/Auth/LoginUserHandler.cs`
- Modify: `src/PriceNegotiationApp.Modules.Identity/Features/Auth/Register.cs`
- Modify: `src/PriceNegotiationApp.Modules.Identity/Features/Auth/Login.cs`
- Modify: `src/PriceNegotiationApp.Modules.Identity/IdentityModule.cs` (2 registrations)

**Interfaces:**
- Consumes: `UserManager<ApplicationUser>`, `JwtManager` (both already DI-registered), `DbWriteGuard.IsUniqueViolation`.
- Produces:
  - `Task<RegistrationResponse> RegisterUserHandler.HandleAsync(RegisterRequest, CancellationToken)`
  - `Task<AuthResponse> LoginUserHandler.HandleAsync(LoginRequest, CancellationToken)`

- [ ] **Step 1: RegisterUserHandler**

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Modules.Identity.Persistence;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Identity.Features.Auth;

internal sealed class RegisterUserHandler(UserManager<ApplicationUser> userManager)
{
    public async Task<RegistrationResponse> HandleAsync(RegisterRequest request, CancellationToken ct)
    {
        var user = new ApplicationUser { UserName = request.Email, Email = request.Email };
        IdentityResult result;
        try
        {
            result = await userManager.CreateAsync(user, request.Password);
        }
        catch (DbUpdateException ex) when (DbWriteGuard.IsUniqueViolation(ex, out _))
        {
            // Two concurrent registrations for the same email: Identity's pre-check
            // lost the race, the unique index caught it — same conflict as usual.
            throw new ConflictException(IdentityErrorCodes.EmailAlreadyRegistered,
                "Email already registered.");
        }

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
        return new RegistrationResponse(user.Id);
    }
}
```

- [ ] **Step 2: LoginUserHandler**

```csharp
using Microsoft.AspNetCore.Identity;
using PriceNegotiationApp.Modules.Identity.Persistence;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Identity.Features.Auth;

internal sealed class LoginUserHandler(UserManager<ApplicationUser> userManager, JwtManager jwt)
{
    public async Task<AuthResponse> HandleAsync(LoginRequest request, CancellationToken ct)
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
        var (token, expiresAtUtc) = jwt.Generate(user.Id, request.Email, roles);
        return new AuthResponse(token, expiresAtUtc, request.Email, roles);
    }
}
```

- [ ] **Step 3: Slim endpoints + register**

`Register.cs` lambda becomes:

```csharp
        group.MapPost("/register", async (RegisterRequest request,
                RegisterUserHandler handler, CancellationToken ct) =>
            TypedResults.Created("/api/v1/auth/me", await handler.HandleAsync(request, ct)))
```

(keep `.RequireRateLimiting(Policies.AuthRateLimitPolicy)` and `.AllowAnonymous()`; drop
the UserManager/Identity usings that become unused).

`Login.cs` lambda becomes:

```csharp
        group.MapPost("/login", async (LoginRequest request, LoginUserHandler handler,
                CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(request, ct)))
```

(keep rate-limiting/anonymous attributes).

In `IdentityModule.AddIdentityModule`, add:

```csharp
        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<LoginUserHandler>();
```

(`Me.cs`, health endpoints untouched.)

- [ ] **Step 4: Validate + commit**

```powershell
dotnet build 2>&1 | Out-Null; if ($LASTEXITCODE -eq 0) { dotnet test tests/PriceNegotiationApp.IntegrationTests --no-build --filter-class PriceNegotiationApp.IntegrationTests.AuthFlowShould 2>&1 | Select-String -Pattern 'failed:|succeeded:' | Select-Object -First 2 } else { dotnet build 2>&1 | Select-String error | Select-Object -First 8 }
git add src/PriceNegotiationApp.Modules.Identity
git commit -m "refactor(identity): register and login handlers own identity infrastructure"
```

---

### Task 5: Enforcement rule, README law, full CI parity

**Files:**
- Modify: `tests/PriceNegotiationApp.ArchitectureTests/ArchitectureShould.cs`
- Modify: `README.md`

**Interfaces:**
- Consumes existing providers (`EntityFramework`, `PersistenceNamespaces`) and ArchUnitNET fluent chain.

- [ ] **Step 1: Endpoints-stay-transport-only architecture fact**

Append inside `ArchitectureShould`:

```csharp
    [Fact]
    public void Endpoint_mapping_types_stay_transport_only()
    {
        var endpoints = Types().That().HaveFullNameEndingWith("Endpoints").As("endpoint mapping types");

        endpoints.Should().NotDependOnAny(EntityFramework).Check(Architecture);
        endpoints.Should().NotDependOnAny(PersistenceNamespaces).Check(Architecture);
    }
```

If any `*Endpoints` type still references EF/persistence after Tasks 1–4 this fact fails
naming the offender.

- [ ] **Step 2: README law**

Under *Tactical DDD laws*, add first bullet:

```markdown
- Endpoints are transport adapters: routing, auth attributes and status shaping only.
  Application logic lives in per-operation `*Handler` services under `Features/`.
```

- [ ] **Step 3: Full CI parity**

```powershell
dotnet format --verify-no-changes --no-restore
dotnet build -c Release --no-restore
dotnet test --solution PriceNegotiationApp.slnx -c Release --no-build --report-trx
```

All five projects green. Fix formatting with `dotnet format` before committing.

- [ ] **Step 4: Commit**

```bash
git add tests/PriceNegotiationApp.ArchitectureTests/ArchitectureShould.cs README.md
git commit -m "test(architecture): enforce transport-only endpoints; document handler law"
```

---

## Self-Review Record

- Spec §3 scope matrix → Tasks 1 (5 write), 2 (Get/List/ListMine), 3 (Catalog ×5),
  4 (Register/Login); `Me`, health, seeding, composition-root adapter explicitly excluded ✓.
- Spec §2 rules → endpoint slimming steps keep TypedResults/auth/cache/rate attrs;
  handlers own queries/save/conflict translation; CallerContext boundary; AddScoped
  explicit registrations ✓.
- Spec §4 enforcement → Task 5 Step 1 ✓; README law Task 5 Step 2 ✓.
- Spec §5 regression harness → per-task integration runs + Task 5 full suite w/ TRX ✓.
- Type consistency sweep: handler names/methods match between creation, registration,
  endpoint resolution and arch-fact naming (`HaveFullNameEndingWith("Endpoints")` matches
  the three real classes only).

