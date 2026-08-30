# Move Endpoints to API Layer

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move all HTTP endpoint mapping code from application modules to the API layer, making modules delivery-mechanism agnostic.

**Architecture:** All 16 individual endpoint files and 3 aggregator files move from `Modules.*/Features/*/` to `Api/Endpoints/`. Modules retain only handlers, DTOs, domain logic, and persistence. The API layer owns the full HTTP contract.

**Tech Stack:** C# 12, .NET 10, ASP.NET Core Minimal APIs, Meziantou.Analyzer (MA0049)

## Global Constraints

- File-scoped namespaces throughout
- `internal` visibility for all module-internal types
- `internal static` for endpoint classes (same assembly, no InternalsVisibleTo needed)
- Endpoint classes use `Endpoint` suffix to satisfy MA0049
- Follow existing code conventions (no comments unless asked)
- All 11 projects must build with 0 errors and 0 warnings

---

## File Structure

### Files to Create (API layer)

```
PriceNegotiationApp.Api/Endpoints/
  Catalog/
    CreateEndpoint.cs
    DeleteEndpoint.cs
    GetEndpoint.cs
    ListEndpoint.cs
    UpdateEndpoint.cs
    CatalogEndpoints.cs
  Negotiations/
    AcceptEndpoint.cs
    CounterProposeEndpoint.cs
    CreateEndpoint.cs
    GetEndpoint.cs
    ListEndpoint.cs
    ListMineEndpoint.cs
    RejectCurrentOfferEndpoint.cs
    WithdrawEndpoint.cs
    NegotiationEndpoints.cs
  Identity/
    LoginEndpoint.cs
    RegisterEndpoint.cs
    MeEndpoint.cs
    IdentityEndpoints.cs
```

### Files to Delete (from modules)

```
Modules.Catalog/Features/Products/Create/CreateEndpoint.cs
Modules.Catalog/Features/Products/Delete/DeleteEndpoint.cs
Modules.Catalog/Features/Products/Get/GetEndpoint.cs
Modules.Catalog/Features/Products/List/ListEndpoint.cs
Modules.Catalog/Features/Products/Update/UpdateEndpoint.cs
Modules.Catalog/CatalogEndpoints.cs

Modules.Negotiations/Features/Negotiations/Accept/AcceptEndpoint.cs
Modules.Negotiations/Features/Negotiations/CounterPropose/CounterProposeEndpoint.cs
Modules.Negotiations/Features/Negotiations/Create/CreateEndpoint.cs
Modules.Negotiations/Features/Negotiations/Get/GetEndpoint.cs
Modules.Negotiations/Features/Negotiations/List/ListEndpoint.cs
Modules.Negotiations/Features/Negotiations/ListMine/ListMineEndpoint.cs
Modules.Negotiations/Features/Negotiations/RejectCurrentOffer/RejectCurrentOfferEndpoint.cs
Modules.Negotiations/Features/Negotiations/Withdraw/WithdrawEndpoint.cs
Modules.Negotiations/NegotiationEndpoints.cs

Modules.Identity/Features/Auth/Login/LoginEndpoint.cs
Modules.Identity/Features/Auth/Register/RegisterEndpoint.cs
Modules.Identity/Features/Auth/Me/MeEndpoint.cs
Modules.Identity/IdentityEndpoints.cs
```

### Files to Modify

- `Modules.Catalog/PriceNegotiationApp.Modules.Catalog.csproj` — remove `FrameworkReference`
- `Modules.Negotiations/PriceNegotiationApp.Modules.Negotiations.csproj` — remove `FrameworkReference`
- `Modules.Identity/PriceNegotiationApp.Modules.Identity.csproj` — remove `FrameworkReference`
- `Modules.Identity/IdentityModule.cs` — remove unused ASP.NET Core using statements
- `Api/Extensions/PipelineExtensions.cs` — update using statements, remove old aggregator calls

---

## Task 1: Move Catalog endpoints to API layer

**Files:**
- Create: `src/PriceNegotiationApp.Api/Endpoints/Catalog/CreateEndpoint.cs`
- Create: `src/PriceNegotiationApp.Api/Endpoints/Catalog/DeleteEndpoint.cs`
- Create: `src/PriceNegotiationApp.Api/Endpoints/Catalog/GetEndpoint.cs`
- Create: `src/PriceNegotiationApp.Api/Endpoints/Catalog/ListEndpoint.cs`
- Create: `src/PriceNegotiationApp.Api/Endpoints/Catalog/UpdateEndpoint.cs`
- Create: `src/PriceNegotiationApp.Api/Endpoints/Catalog/CatalogEndpoints.cs`
- Delete: All 5 endpoint files + `CatalogEndpoints.cs` from the module

**Interfaces:**
- Consumes: `CreateProductHandler`, `DeleteProductHandler`, `GetProductHandler`, `ListProductsHandler`, `UpdateProductHandler` (all `internal sealed` in Catalog module, resolved via DI)
- Consumes: `CreateProductRequest`, `UpdateProductRequest`, `ProductQuery` (internal DTOs in Catalog module)
- Consumes: `UserRoles`, `Policies` (public in SharedKernel)
- Produces: `MapCatalogEndpoints()` extension method (called from `PipelineExtensions.cs`)

- [ ] **Step 1: Create API endpoint directory**

```bash
New-Item -ItemType Directory -Force -Path "src/PriceNegotiationApp.Api/Endpoints/Catalog"
```

- [ ] **Step 2: Create endpoint files in API layer**

Each file gets namespace `PriceNegotiationApp.Api.Endpoints.Catalog.*` and adds `using` for the handler/DTO types from the Catalog module.

`CreateEndpoint.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Catalog.Features.Products;
using PriceNegotiationApp.Modules.Catalog.Features.Products.Create;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Api.Endpoints.Catalog.Create;

internal static class CreateEndpoint
{
    internal static void MapCreate(this RouteGroupBuilder group)
    {
        group.MapPost("/", async (CreateProductRequest request, CreateProductHandler handler,
                CancellationToken ct) =>
            {
                var response = await handler.HandleAsync(request, ct);
                return TypedResults.CreatedAtRoute(response, "GetProductById", new { id = response.Id });
            })
        .RequireRoles(UserRoles.Admin, UserRoles.Staff);
    }
}
```

`DeleteEndpoint.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Catalog.Features.Products.Delete;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Api.Endpoints.Catalog.Delete;

internal static class DeleteEndpoint
{
    internal static void MapDelete(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", async (Guid id, DeleteProductHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(id, ct);
            return TypedResults.NoContent();
        })
        .RequireRoles(UserRoles.Admin);
    }
}
```

`GetEndpoint.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PriceNegotiationApp.Modules.Catalog.Features.Products.Get;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Api.Endpoints.Catalog.Get;

internal static class GetEndpoint
{
    internal static void MapGetOne(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", async (Guid id, GetProductHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(id, ct)))
            .WithName("GetProductById")
            .CacheOutput(Policies.ShortCachePolicy)
            .AllowAnonymous();
    }
}
```

`ListEndpoint.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PriceNegotiationApp.Modules.Catalog.Features.Products;
using PriceNegotiationApp.Modules.Catalog.Features.Products.List;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Api.Endpoints.Catalog.List;

internal static class ListEndpoint
{
    internal static void MapList(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (ListProductsHandler handler, CancellationToken ct,
                string? search = null, decimal? minPrice = null, decimal? maxPrice = null,
                string? sortBy = null, bool sortDesc = false, int page = 1, int pageSize = 20) =>
                TypedResults.Ok(await handler.HandleAsync(
                    new ProductQuery(search, minPrice, maxPrice, sortBy, sortDesc, page, pageSize), ct)))
            .CacheOutput(Policies.ShortCachePolicy)
            .AllowAnonymous();
    }
}
```

`UpdateEndpoint.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Catalog.Features.Products;
using PriceNegotiationApp.Modules.Catalog.Features.Products.Update;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Api.Endpoints.Catalog.Update;

internal static class UpdateEndpoint
{
    internal static void MapUpdate(this RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", async (Guid id, UpdateProductRequest request,
                UpdateProductHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(id, request, ct)))
        .RequireRoles(UserRoles.Admin, UserRoles.Staff);
    }
}
```

`CatalogEndpoints.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Api.Endpoints.Catalog.Create;
using PriceNegotiationApp.Api.Endpoints.Catalog.Delete;
using PriceNegotiationApp.Api.Endpoints.Catalog.Get;
using PriceNegotiationApp.Api.Endpoints.Catalog.List;
using PriceNegotiationApp.Api.Endpoints.Catalog.Update;

namespace PriceNegotiationApp.Api.Endpoints.Catalog;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/products")
            .WithTags("Products")
            .RequireAuthorization();
        group.MapList();
        group.MapGetOne();
        group.MapCreate();
        group.MapUpdate();
        group.MapDelete();
        return app;
    }
}
```

- [ ] **Step 3: Delete old endpoint files from Catalog module**

```bash
Remove-Item "src/PriceNegotiationApp.Modules.Catalog/Features/Products/Create/CreateEndpoint.cs"
Remove-Item "src/PriceNegotiationApp.Modules.Catalog/Features/Products/Delete/DeleteEndpoint.cs"
Remove-Item "src/PriceNegotiationApp.Modules.Catalog/Features/Products/Get/GetEndpoint.cs"
Remove-Item "src/PriceNegotiationApp.Modules.Catalog/Features/Products/List/ListEndpoint.cs"
Remove-Item "src/PriceNegotiationApp.Modules.Catalog/Features/Products/Update/UpdateEndpoint.cs"
Remove-Item "src/PriceNegotiationApp.Modules.Catalog/CatalogEndpoints.cs"
```

- [ ] **Step 4: Update PipelineExtensions.cs using statements**

Replace:
```csharp
using PriceNegotiationApp.Modules.Catalog;
```
With:
```csharp
using PriceNegotiationApp.Api.Endpoints.Catalog;
```

- [ ] **Step 5: Run build validation**

```bash
dotnet build --no-restore
```

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "refactor: move Catalog endpoints from module to API layer"
```

---

## Task 2: Move Negotiations endpoints to API layer

**Files:**
- Create: 8 endpoint files + `NegotiationEndpoints.cs` in `Api/Endpoints/Negotiations/`
- Delete: All 8 endpoint files + `NegotiationEndpoints.cs` from the module

**Interfaces:**
- Consumes: All 8 handlers (internal sealed in Negotiations module, resolved via DI)
- Consumes: `CreateNegotiationRequest`, `CounterProposalRequest`, `NegotiationModels` (internal DTOs)
- Consumes: `UserRoles`, `PageQuery`, `CallerContextExtensions` (public in SharedKernel)
- Produces: `MapNegotiationsEndpoints()` extension method

- [ ] **Step 1: Create API endpoint directory**

```bash
New-Item -ItemType Directory -Force -Path "src/PriceNegotiationApp.Api/Endpoints/Negotiations"
```

- [ ] **Step 2: Create all 9 endpoint files in API layer**

Each file gets namespace `PriceNegotiationApp.Api.Endpoints.Negotiations.*`.

`CreateEndpoint.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;
using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.Create;
using PriceNegotiationApp.SharedKernel;
using System.Security.Claims;

namespace PriceNegotiationApp.Api.Endpoints.Negotiations.Create;

internal static class CreateEndpoint
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

`GetEndpoint.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.Get;
using System.Security.Claims;

namespace PriceNegotiationApp.Api.Endpoints.Negotiations.Get;

internal static class GetEndpoint
{
    internal static void MapGetOne(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal principal,
                GetNegotiationHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(id, principal.ToCallerContext(), ct)));
    }
}
```

`ListEndpoint.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.List;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Api.Endpoints.Negotiations.List;

internal static class ListEndpoint
{
    internal static void MapList(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (ListNegotiationsHandler handler, CancellationToken ct,
                int page = 1, int pageSize = 20) =>
            TypedResults.Ok(await handler.HandleAsync(new PageQuery(page, pageSize), ct)))
        .RequireRoles(UserRoles.Admin, UserRoles.Staff);
    }
}
```

`ListMineEndpoint.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.ListMine;
using PriceNegotiationApp.SharedKernel;
using System.Security.Claims;

namespace PriceNegotiationApp.Api.Endpoints.Negotiations.ListMine;

internal static class ListMineEndpoint
{
    internal static void MapListMine(this RouteGroupBuilder group)
    {
        group.MapGet("/mine", async (ClaimsPrincipal principal, ListMyNegotiationsHandler handler,
                CancellationToken ct, int page = 1, int pageSize = 20) =>
            TypedResults.Ok(await handler.HandleAsync(
                new PageQuery(page, pageSize), principal.ToCallerContext(), ct)))
        .RequireRoles(UserRoles.Customer);
    }
}
```

`AcceptEndpoint.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.Accept;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Api.Endpoints.Negotiations.Accept;

internal static class AcceptEndpoint
{
    internal static void MapAccept(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/accept", async (Guid id, AcceptHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(id, ct)))
        .RequireRoles(UserRoles.Admin, UserRoles.Staff);
    }
}
```

`CounterProposeEndpoint.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;
using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.CounterPropose;
using System.Security.Claims;

namespace PriceNegotiationApp.Api.Endpoints.Negotiations.CounterPropose;

internal static class CounterProposeEndpoint
{
    internal static void MapCounterPropose(this RouteGroupBuilder group)
    {
        group.MapPatch("/{id:guid}/proposals", async (Guid id, CounterProposalRequest request,
                ClaimsPrincipal principal, CounterProposeHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(id, request, principal.ToCallerContext(), ct)));
    }
}
```

`RejectCurrentOfferEndpoint.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.RejectCurrentOffer;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Api.Endpoints.Negotiations.RejectCurrentOffer;

internal static class RejectCurrentOfferEndpoint
{
    internal static void MapRejectCurrentOffer(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/decline", async (Guid id, RejectCurrentOfferHandler handler,
                CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(id, ct)))
        .RequireRoles(UserRoles.Admin, UserRoles.Staff);
    }
}
```

`WithdrawEndpoint.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.Withdraw;
using System.Security.Claims;

namespace PriceNegotiationApp.Api.Endpoints.Negotiations.Withdraw;

internal static class WithdrawEndpoint
{
    internal static void MapWithdraw(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal principal,
                WithdrawHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(id, principal.ToCallerContext(), ct);
            return TypedResults.NoContent();
        });
    }
}
```

`NegotiationEndpoints.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Api.Endpoints.Negotiations.Accept;
using PriceNegotiationApp.Api.Endpoints.Negotiations.CounterPropose;
using PriceNegotiationApp.Api.Endpoints.Negotiations.Create;
using PriceNegotiationApp.Api.Endpoints.Negotiations.Get;
using PriceNegotiationApp.Api.Endpoints.Negotiations.List;
using PriceNegotiationApp.Api.Endpoints.Negotiations.ListMine;
using PriceNegotiationApp.Api.Endpoints.Negotiations.RejectCurrentOffer;
using PriceNegotiationApp.Api.Endpoints.Negotiations.Withdraw;

namespace PriceNegotiationApp.Api.Endpoints.Negotiations;

public static class NegotiationEndpoints
{
    public static IEndpointRouteBuilder MapNegotiationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/negotiations")
            .WithTags("Negotiations")
            .RequireAuthorization();
        group.MapCreate();
        group.MapListMine();
        group.MapList();
        group.MapGetOne();
        group.MapCounterPropose();
        group.MapAccept();
        group.MapRejectCurrentOffer();
        group.MapWithdraw();
        return app;
    }
}
```

- [ ] **Step 3: Delete old endpoint files from Negotiations module**

```bash
Remove-Item "src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Accept/AcceptEndpoint.cs"
Remove-Item "src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/CounterPropose/CounterProposeEndpoint.cs"
Remove-Item "src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Create/CreateEndpoint.cs"
Remove-Item "src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Get/GetEndpoint.cs"
Remove-Item "src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/List/ListEndpoint.cs"
Remove-Item "src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/ListMine/ListMineEndpoint.cs"
Remove-Item "src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/RejectCurrentOffer/RejectCurrentOfferEndpoint.cs"
Remove-Item "src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Withdraw/WithdrawEndpoint.cs"
Remove-Item "src/PriceNegotiationApp.Modules.Negotiations/NegotiationEndpoints.cs"
```

- [ ] **Step 4: Update PipelineExtensions.cs using statements**

Replace:
```csharp
using PriceNegotiationApp.Modules.Negotiations;
```
With:
```csharp
using PriceNegotiationApp.Api.Endpoints.Negotiations;
```

- [ ] **Step 5: Run build validation**

```bash
dotnet build --no-restore
```

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "refactor: move Negotiations endpoints from module to API layer"
```

---

## Task 3: Move Identity endpoints to API layer

**Files:**
- Create: 3 endpoint files + `IdentityEndpoints.cs` in `Api/Endpoints/Identity/`
- Delete: All 3 endpoint files + `IdentityEndpoints.cs` from the module

**Interfaces:**
- Consumes: `LoginUserHandler`, `RegisterUserHandler` (internal sealed in Identity module, resolved via DI)
- Consumes: `LoginRequest`, `RegisterRequest`, `CurrentUserResponse` (internal DTOs)
- Consumes: `Policies`, `CallerContextExtensions` (public in SharedKernel)
- Produces: `MapAuthEndpoints()` extension method

- [ ] **Step 1: Create API endpoint directory**

```bash
New-Item -ItemType Directory -Force -Path "src/PriceNegotiationApp.Api/Endpoints/Identity"
```

- [ ] **Step 2: Create all 4 endpoint files in API layer**

`LoginEndpoint.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Identity.Features.Auth;
using PriceNegotiationApp.Modules.Identity.Features.Auth.Login;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Api.Endpoints.Identity.Login;

internal static class LoginEndpoint
{
    internal static void MapLogin(this RouteGroupBuilder group)
    {
        group.MapPost("/login", async (LoginRequest request, LoginUserHandler handler,
                CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(request)))
        .RequireRateLimiting(Policies.AuthRateLimitPolicy)
        .AllowAnonymous()
        .WithName("Login")
        .WithSummary("Authenticate and issue an access token")
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }
}
```

`RegisterEndpoint.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Identity.Features.Auth;
using PriceNegotiationApp.Modules.Identity.Features.Auth.Register;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Api.Endpoints.Identity.Register;

internal static class RegisterEndpoint
{
    internal static void MapRegister(this RouteGroupBuilder group)
    {
        group.MapPost("/register", async (RegisterRequest request,
                RegisterUserHandler handler, CancellationToken ct) =>
            TypedResults.Created("/api/v1/auth/me", await handler.HandleAsync(request)))
        .RequireRateLimiting(Policies.AuthRateLimitPolicy)
        .AllowAnonymous()
        .WithName("RegisterUser")
        .WithSummary("Register a new customer account")
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }
}
```

`MeEndpoint.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Identity.Features.Auth;
using PriceNegotiationApp.SharedKernel;
using System.Security.Claims;

namespace PriceNegotiationApp.Api.Endpoints.Identity.Me;

internal static class MeEndpoint
{
    internal static void MapMe(this RouteGroupBuilder group)
    {
        group.MapGet("/me", (ClaimsPrincipal principal) =>
            {
                var caller = principal.ToCallerContext();
                return TypedResults.Ok(new CurrentUserResponse(caller.UserId, caller.Email, caller.Roles.ToList()));
            })
        .WithName("GetCurrentUser")
        .WithSummary("Return the authenticated caller's profile");
    }
}
```

`IdentityEndpoints.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Api.Endpoints.Identity.Login;
using PriceNegotiationApp.Api.Endpoints.Identity.Me;
using PriceNegotiationApp.Api.Endpoints.Identity.Register;

namespace PriceNegotiationApp.Api.Endpoints.Identity;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth")
            .WithTags("Auth")
            .RequireAuthorization();
        group.MapRegister();
        group.MapLogin();
        group.MapMe();
        return app;
    }
}
```

- [ ] **Step 3: Delete old endpoint files from Identity module**

```bash
Remove-Item "src/PriceNegotiationApp.Modules.Identity/Features/Auth/Login/LoginEndpoint.cs"
Remove-Item "src/PriceNegotiationApp.Modules.Identity/Features/Auth/Register/RegisterEndpoint.cs"
Remove-Item "src/PriceNegotiationApp.Modules.Identity/Features/Auth/Me/MeEndpoint.cs"
Remove-Item "src/PriceNegotiationApp.Modules.Identity/IdentityEndpoints.cs"
```

- [ ] **Step 4: Update PipelineExtensions.cs using statements**

Replace:
```csharp
using PriceNegotiationApp.Modules.Identity;
```
With:
```csharp
using PriceNegotiationApp.Api.Endpoints.Identity;
```

- [ ] **Step 5: Run build validation**

```bash
dotnet build --no-restore
```

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "refactor: move Identity endpoints from module to API layer"
```

---

## Task 4: Remove ASP.NET Core FrameworkReference from module csprojs

**Files:**
- Modify: `src/PriceNegotiationApp.Modules.Catalog/PriceNegotiationApp.Modules.Catalog.csproj`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/PriceNegotiationApp.Modules.Negotiations.csproj`
- Modify: `src/PriceNegotiationApp.Modules.Identity/PriceNegotiationApp.Modules.Identity.csproj`

**Interfaces:**
- Consumes: Nothing (cleanup step)
- Produces: Modules no longer depend on `Microsoft.AspNetCore.App` FrameworkReference

- [ ] **Step 1: Remove FrameworkReference from Catalog csproj**

Remove this block from `PriceNegotiationApp.Modules.Catalog.csproj`:
```xml
<FrameworkReference Include="Microsoft.AspNetCore.App" />
```

- [ ] **Step 2: Remove FrameworkReference from Negotiations csproj**

Remove this block from `PriceNegotiationApp.Modules.Negotiations.csproj`:
```xml
<FrameworkReference Include="Microsoft.AspNetCore.App" />
```

- [ ] **Step 3: Remove FrameworkReference from Identity csproj**

Remove this block from `PriceNegotiationApp.Modules.Identity.csproj`:
```xml
<FrameworkReference Include="Microsoft.AspNetCore.App" />
```

- [ ] **Step 4: Clean unused ASP.NET Core using statements from IdentityModule.cs**

Remove these unused using statements from `IdentityModule.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
```

- [ ] **Step 5: Run build validation**

```bash
dotnet build --no-restore
```

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "refactor: remove ASP.NET Core FrameworkReference from module csprojs"
```

---

## Task 5: Clean up empty directories left behind

**Files:**
- Delete: Empty use-case subdirectories in all 3 modules

- [ ] **Step 1: Remove empty Catalog feature subdirectories**

```bash
Remove-Item "src/PriceNegotiationApp.Modules.Catalog/Features/Products/Create" -Recurse -Force
Remove-Item "src/PriceNegotiationApp.Modules.Catalog/Features/Products/Delete" -Recurse -Force
Remove-Item "src/PriceNegotiationApp.Modules.Catalog/Features/Products/Get" -Recurse -Force
Remove-Item "src/PriceNegotiationApp.Modules.Catalog/Features/Products/List" -Recurse -Force
Remove-Item "src/PriceNegotiationApp.Modules.Catalog/Features/Products/Update" -Recurse -Force
```

- [ ] **Step 2: Remove empty Negotiations feature subdirectories**

```bash
Remove-Item "src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Accept" -Recurse -Force
Remove-Item "src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/CounterPropose" -Recurse -Force
Remove-Item "src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Create" -Recurse -Force
Remove-Item "src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Get" -Recurse -Force
Remove-Item "src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/List" -Recurse -Force
Remove-Item "src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/ListMine" -Recurse -Force
Remove-Item "src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/RejectCurrentOffer" -Recurse -Force
Remove-Item "src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Withdraw" -Recurse -Force
```

- [ ] **Step 3: Remove empty Identity feature subdirectories**

```bash
Remove-Item "src/PriceNegotiationApp.Modules.Identity/Features/Auth/Login" -Recurse -Force
Remove-Item "src/PriceNegotiationApp.Modules.Identity/Features/Auth/Register" -Recurse -Force
Remove-Item "src/PriceNegotiationApp.Modules.Identity/Features/Auth/Me" -Recurse -Force
Remove-Item "src/PriceNegotiationApp.Modules.Identity/Auth" -Recurse -Force
```

- [ ] **Step 4: Run full build validation**

```bash
dotnet build --no-restore
```

- [ ] **Step 5: Run tests**

```bash
dotnet test --no-build
```

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "refactor: clean up empty directories after endpoint migration"
```

---

## Self-Review Checklist

1. **Spec coverage:** All 16 endpoint files + 3 aggregator files moved. All module csprojs cleaned. All empty dirs removed.
2. **Placeholder scan:** No TBD/TODO. All code blocks are complete.
3. **Type consistency:** Handler class names, DTO names, and extension method names are consistent across all tasks. The `PipelineExtensions.cs` using updates match the new namespaces.
4. **Architecture test compatibility:** Rule #9 (`Endpoint_mapping_types_stay_transport_only`) targets types ending with `"Endpoints"`. The new `CatalogEndpoints.cs`, `NegotiationEndpoints.cs`, `IdentityEndpoints.cs` are in the API assembly (loaded as `typeof(GlobalExceptionHandler).Assembly`). The rule should still pass since these types only reference ASP.NET Core and handler types, not EF Core or Persistence.
5. **InternalsVisibleTo:** All 3 modules keep `InternalsVisibleTo Include="PriceNegotiationApp.Api"` so the API can reference internal handler types in endpoint lambda parameters.
