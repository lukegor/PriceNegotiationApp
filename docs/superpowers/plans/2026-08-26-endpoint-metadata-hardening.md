# Endpoint Metadata & Security-Surface Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Flip API modules to secure-by-default route groups, wire the dead CORS configuration, document every endpoint's error contract in OpenAPI (`ProducesProblem`), give every route a stable name and summary, and keep infrastructure endpoints out of the published API description.

**Architecture:** All 16 business endpoints live in one file per feature under `src/PriceNegotiationApp.Modules.*/Features/**`, registered through three module entry points. Authorization moves from per-endpoint opt-in to group-level `.RequireAuthorization()` with explicit `.AllowAnonymous()` opt-outs. Error documentation uses built-in `.ProducesProblem(statusCode)` chains — success schemas are already inferred from `TypedResults`. No new abstractions; `SharedKernel.EndpointConventionExtensions` stays untouched.

**Tech Stack:** ASP.NET Core 10 minimal APIs, `Microsoft.AspNetCore.OpenApi` (runtime docs, asserted in tests via `/openapi/v1.json`), xUnit + Shouldly + Testcontainers (Postgres) for integration tests.

## Global Constraints

- Target framework: `net10.0` (set in `Directory.Build.props`; do not change).
- Central Package Management via `Directory.Packages.props` — **add zero NuGet packages**; everything used ships in the shared framework.
- Analyzers run as errors: code must compile with zero warnings.
- No comments in code unless mirroring an existing comment convention in the same file.
- Commit style: conventional commits matching repo history — `fix(api): ...`, `feat(negotiations): ...`, `test(integration): ...`.
- Integration tests need Docker running (Testcontainers starts `postgres:17-alpine`).
- Test frameworks in use: xUnit (`[Fact]`, `[Theory]`, `[Collection]`), Shouldly (`ShouldBe`, `ShouldNotBeNull`, ...), `TestContext.Current.CancellationToken` passed to every async call.
- Documentation rule for every endpoint (from approved design): document every **handler-raised** problem status; omit generic 401 (implied by bearer security scheme) everywhere except where login itself fails (401 IS the business outcome); add 429 only on rate-limited endpoints.
- Route names (`WithName`) are app-wide unique PascalCase verb-noun identifiers; they become OpenAPI `operationId`s.

## File Structure

| File | Change |
|---|---|
| `src/PriceNegotiationApp.Modules.Identity/IdentityEndpoints.cs` | Group-level `.RequireAuthorization()` |
| `src/PriceNegotiationApp.Modules.Catalog/CatalogEndpoints.cs` | Group-level `.RequireAuthorization()` |
| `src/PriceNegotiationApp.Modules.Negotiations/NegotiationEndpoints.cs` | Group-level `.RequireAuthorization()` |
| `src/PriceNegotiationApp.Modules.Identity/Features/Auth/Register.cs` | Remove redundant authz (T1); add name/summary/problems (T3) |
| `src/PriceNegotiationApp.Modules.Identity/Features/Auth/Login.cs` | Same as Register.cs |
| `src/PriceNegotiationApp.Modules.Identity/Features/Auth/Me.cs` | Drop per-endpoint authz (T1); add name/summary (T3) |
| `src/PriceNegotiationApp.Modules.Catalog/Features/Products/List.cs` | Name/summary (T4) |
| `src/PriceNegotiationApp.Modules.Catalog/Features/Products/Get.cs` | Summary added, name kept (T4) |
| `src/PriceNegotiationApp.Modules.Catalog/Features/Products/Create.cs` | Name/summary/problems (T4) |
| `src/PriceNegotiationApp.Modules.Catalog/Features/Products/Update.cs` | Name/summary/problems (T4) |
| `src/PriceNegotiationApp.Modules.Catalog/Features/Products/Delete.cs` | Name/summary/problems (T4) |
| `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Create.cs` | Name/summary/problems (T5) |
| `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/ListMine.cs` | Name/summary (T5) |
| `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/List.cs` | Name/summary (T5) |
| `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Get.cs` | Drop per-endpoint authz (T1); name/summary/problems (T5) |
| `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/CounterPropose.cs` | Drop per-endpoint authz (T1); name/summary/problems (T5) |
| `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Accept.cs` | Name/summary/problems (T5) |
| `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/RejectCurrentOffer.cs` | Name/summary/problems (T5) |
| `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Withdraw.cs` | Drop per-endpoint authz (T1); name/summary/problems (T5) |
| `src/PriceNegotiationApp.Api/Extensions/WebApplicationBuilderExtensions.cs` | Always register CORS policy; vary product cache by Origin (T2) |
| `src/PriceNegotiationApp.Api/Extensions/PipelineExtensions.cs` | `UseCors` (T2); `ExcludeFromDescription` + Testing-env OpenAPI (T6) |
| `tests/PriceNegotiationApp.IntegrationTests/Support/IntegrationTestFactory.cs` | Configure allowed CORS origin (T2) |
| `tests/PriceNegotiationApp.IntegrationTests/PublicSurfaceShould.cs` | Create (T1) |
| `tests/PriceNegotiationApp.IntegrationTests/CorsShould.cs` | Create (T2) |
| `tests/PriceNegotiationApp.IntegrationTests/OpenApiContractShould.cs` | Create (T7) |

---

### Task 1: Secure-by-default authorization groups + public-surface lock test

**Files:**
- Modify: `src/PriceNegotiationApp.Modules.Identity/IdentityEndpoints.cs`
- Modify: `src/PriceNegotiationApp.Modules.Catalog/CatalogEndpoints.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/NegotiationEndpoints.cs`
- Modify: `src/PriceNegotiationApp.Modules.Identity/Features/Auth/Me.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Get.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/CounterPropose.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Withdraw.cs`
- Create: `tests/PriceNegotiationApp.IntegrationTests/PublicSurfaceShould.cs`

**Interfaces:**
- Consumes: nothing new; existing `UserRoles`/`Policies` constants stay as-is.
- Produces: authorization semantics consumed by every later task — groups require auth; only register/login/product-list/product-get-one/jwks opt out via `.AllowAnonymous()`. Later tasks must NOT re-add `.RequireAuthorization()` per endpoint.

- [ ] **Step 1: Add group-level authorization to the three module entry points**

Replace the body of `MapAuthEndpoints` in `src/PriceNegotiationApp.Modules.Identity/IdentityEndpoints.cs`:

```csharp
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
```

Apply the identical pattern in `src/PriceNegotiationApp.Modules.Catalog/CatalogEndpoints.cs`:

```csharp
var group = app.MapGroup("/api/v1/products")
    .WithTags("Products")
    .RequireAuthorization();
```

and in `src/PriceNegotiationApp.Modules.Negotiations/NegotiationEndpoints.cs`:

```csharp
var group = app.MapGroup("/api/v1/negotiations")
    .WithTags("Negotiations")
    .RequireAuthorization();
```

- [ ] **Step 2: Remove now-redundant per-endpoint `.RequireAuthorization()` calls**

Four endpoints carry their own plain `.RequireAuthorization()` which the group now provides. Delete those lines (keep everything else on the endpoint intact):

`src/PriceNegotiationApp.Modules.Identity/Features/Auth/Me.cs` — the lambda ends with `})`, followed by the deleted `.RequireAuthorization();` line:

```csharp
group.MapGet("/me", (ClaimsPrincipal principal) =>
    {
        var caller = principal.ToCallerContext();
        return TypedResults.Ok(new CurrentUserResponse(caller.UserId, caller.Email, caller.Roles.ToList()));
    });
```

`src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Get.cs`:

```csharp
group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal principal,
        GetNegotiationHandler handler, CancellationToken ct) =>
    TypedResults.Ok(await handler.HandleAsync(id, principal.ToCallerContext(), ct)));
```

`src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/CounterPropose.cs`:

```csharp
group.MapPatch("/{id:guid}/proposals", async (Guid id, CounterProposalRequest request,
        ClaimsPrincipal principal, CounterProposeHandler handler, CancellationToken ct) =>
    TypedResults.Ok(await handler.HandleAsync(id, request, principal.ToCallerContext(), ct)));
```

`src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Withdraw.cs`:

```csharp
group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal principal,
        WithdrawHandler handler, CancellationToken ct) =>
{
    await handler.HandleAsync(id, principal.ToCallerContext(), ct);
    return TypedResults.NoContent();
});
```

Do NOT touch `.AllowAnonymous()` (Register, Login, catalog List/Get) or any `.RequireRoles(...)` call — they compose correctly with the group requirement (all authorize data merges; user must satisfy every layer).

- [ ] **Step 3: Add the public-surface lock test**

Create `tests/PriceNegotiationApp.IntegrationTests/PublicSurfaceShould.cs`. This pins the exact set of unauthenticated-reachable routes: any future endpoint added without opting out of group auth makes this theory fail.

```csharp
using PriceNegotiationApp.IntegrationTests.Support;
using Shouldly;
using System.Net;
using System.Text;
using Xunit;

namespace PriceNegotiationApp.IntegrationTests;

[Collection(ApiCollection.Name)]
public class PublicSurfaceShould(IntegrationTestFixture fixture)
{
    public static TheoryData<HttpMethod, string> ProtectedRoutes => new()
    {
        { HttpMethod.Get, "/api/v1/auth/me" },
        { HttpMethod.Post, "/api/v1/products" },
        { HttpMethod.Put, $"/api/v1/products/{Guid.NewGuid()}" },
        { HttpMethod.Delete, $"/api/v1/products/{Guid.NewGuid()}" },
        { HttpMethod.Post, "/api/v1/negotiations" },
        { HttpMethod.Get, "/api/v1/negotiations/mine" },
        { HttpMethod.Get, "/api/v1/negotiations" },
        { HttpMethod.Get, $"/api/v1/negotiations/{Guid.NewGuid()}" },
        { HttpMethod.Patch, $"/api/v1/negotiations/{Guid.NewGuid()}/proposals" },
        { HttpMethod.Post, $"/api/v1/negotiations/{Guid.NewGuid()}/accept" },
        { HttpMethod.Post, $"/api/v1/negotiations/{Guid.NewGuid()}/decline" },
        { HttpMethod.Delete, $"/api/v1/negotiations/{Guid.NewGuid()}" },
    };

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task Unauthenticated_requests_are_challenged(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json"),
        };

        var response = await fixture.Anonymous.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, $"{method} {path} must stay behind authentication");
    }
}
```

Positive coverage of the five public routes (register 201, login 200, products list/get, jwks 200) already exists in `AuthFlowShould`, `ProductsShould`, and `JwksShould` — do not duplicate it.

- [ ] **Step 4: Run relevant validation**

```bash
dotnet build
dotnet test tests/PriceNegotiationApp.IntegrationTests --filter "FullyQualifiedName~PublicSurfaceShould"
```

Both must pass. If any protected route returns something other than 401, the group wiring is wrong — fix before committing.

- [ ] **Step 5: Commit**

```bash
git add src/PriceNegotiationApp.Modules.Identity src/PriceNegotiationApp.Modules.Catalog src/PriceNegotiationApp.Modules.Negotiations tests/PriceNegotiationApp.IntegrationTests/PublicSurfaceShould.cs
git commit -m "feat(api): secure-by-default route groups with locked public surface"
```

---

### Task 2: Wire the dead CORS configuration

**Files:**
- Modify: `src/PriceNegotiationApp.Api/Extensions/WebApplicationBuilderExtensions.cs`
- Modify: `src/PriceNegotiationApp.Api/Extensions/PipelineExtensions.cs`
- Modify: `tests/PriceNegotiationApp.IntegrationTests/Support/IntegrationTestFactory.cs`
- Create: `tests/PriceNegotiationApp.IntegrationTests/CorsShould.cs`

**Interfaces:**
- Consumes: `WebApplicationBuilderExtensions.CorsPolicy` constant (already `"api"`).
- Produces: working CORS enforcement for any configured origin list; `IntegrationTestFactory` clients speak to the app with `Cors:AllowedOrigins=https://app.test.local` set (later tasks rely on this factory state being harmless).

- [ ] **Step 1: Always register the CORS policy (empty allow-list denies everyone)**

In `WebApplicationBuilderExtensions.AddApiServices`, replace:

```csharp
var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
CorsOriginsGuard.EnsureValid(origins);
if (origins.Length > 0)
{
    builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
}
```

with:

```csharp
var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
CorsOriginsGuard.EnsureValid(origins);
builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
```

An empty origin list produces a policy that matches nobody, so unconfigured deployments behave exactly as today while the policy always exists for the middleware.

- [ ] **Step 2: Vary the product output cache by Origin**

A cached response replays stored headers; without origin variance, the first requester's `Access-Control-Allow-Origin` would leak into other origins' cache hits. In the same file, change the output-cache policy registration:

```csharp
builder.Services.AddOutputCache(options => options.AddPolicy(Policies.ShortCachePolicy,
    policy => policy.Expire(TimeSpan.FromSeconds(30))
        .SetVaryByQuery("search", "minPrice", "maxPrice", "sortBy", "sortDesc", "page", "pageSize")
        .SetVaryByHeader("Origin")));
```

- [ ] **Step 3: Apply the policy in the pipeline**

In `PipelineExtensions.UsePipeline`, insert `UseCors` immediately after `UseHttpsRedirection()` and before the environment-gated OpenAPI block (CORS middleware must precede authentication/authorization):

```csharp
app.UseHttpsRedirection();

app.UseCors(WebApplicationBuilderExtensions.CorsPolicy);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
```

No new `using` needed — both classes live in `PriceNegotiationApp.Api.Extensions`.

- [ ] **Step 4: Configure a known origin for tests**

In `Support/IntegrationTestFactory.ConfigureWebHost`, add alongside the other `UseSetting` lines:

```csharp
builder.UseSetting("Cors:AllowedOrigins", "https://app.test.local");
```

- [ ] **Step 5: Add the CORS test**

Create `tests/PriceNegotiationApp.IntegrationTests/CorsShould.cs`:

```csharp
using PriceNegotiationApp.IntegrationTests.Support;
using Shouldly;
using System.Net;
using Xunit;

namespace PriceNegotiationApp.IntegrationTests;

[Collection(ApiCollection.Name)]
public class CorsShould(IntegrationTestFixture fixture)
{
    private const string AllowedOrigin = "https://app.test.local";

    [Fact]
    public async Task Configured_origin_receives_allow_origin_header()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/products");
        request.Headers.TryAddWithoutValidation("Origin", AllowedOrigin);

        var response = await fixture.Anonymous.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.HeaderAccessControlAllowOrigin.ShouldBe([AllowedOrigin]);
    }

    [Fact]
    public async Task Unlisted_origin_receives_no_allow_origin_header()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/products");
        request.Headers.TryAddWithoutValidation("Origin", "https://evil.example");

        var response = await fixture.Anonymous.SendAsync(request, TestContext.Current.CancellationToken);

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }
}
```

The second test is deterministic because of Step 2: the unlisted origin always misses the output cache and gets a freshly built response with no CORS headers.

- [ ] **Step 6: Run relevant validation**

```bash
dotnet build
dotnet test tests/PriceNegotiationApp.IntegrationTests --filter "FullyQualifiedName~CorsShould"
```

- [ ] **Step 7: Commit**

```bash
git add src/PriceNegotiationApp.Api tests/PriceNegotiationApp.IntegrationTests/Support/IntegrationTestFactory.cs tests/PriceNegotiationApp.IntegrationTests/CorsShould.cs
git commit -m "fix(api): enforce configured cors policy and vary product cache by origin"
```

---

### Task 3: Metadata for the Auth module (names, summaries, problem responses)

**Files:**
- Modify: `src/PriceNegotiationApp.Modules.Identity/Features/Auth/Register.cs`
- Modify: `src/PriceNegotiationApp.Modules.Identity/Features/Auth/Login.cs`
- Modify: `src/PriceNegotiationApp.Modules.Identity/Features/Auth/Me.cs`

**Interfaces:**
- Produces: route names `RegisterUser`, `Login`, `GetCurrentUser`; documented problems 409/422/429 (register), 401/422/429 (login). These names/assertions are consumed verbatim by Task 7's OpenAPI contract test.

- [ ] **Step 1: Rewrite the three endpoint files**

Replace the full content of `src/PriceNegotiationApp.Modules.Identity/Features/Auth/Register.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Identity.Features.Auth;

internal static class Register
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

Replace the full content of `src/PriceNegotiationApp.Modules.Identity/Features/Auth/Login.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Identity.Features.Auth;

internal static class Login
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

For login, 401 IS the documented business outcome of failed authentication (`invalid_credentials` problem), hence it appears despite the general omit-generic-401 rule.

Replace the full content of `src/PriceNegotiationApp.Modules.Identity/Features/Auth/Me.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.SharedKernel;
using System.Security.Claims;

namespace PriceNegotiationApp.Modules.Identity.Features.Auth;

internal static class Me
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

- [ ] **Step 2: Run relevant validation**

```bash
dotnet build
dotnet test tests/PriceNegotiationApp.IntegrationTests --filter "FullyQualifiedName~AuthFlowShould"
```

Existing auth flows must stay green (metadata-only change).

- [ ] **Step 3: Commit**

```bash
git add src/PriceNegotiationApp.Modules.Identity/Features/Auth
git commit -m "feat(identity): document auth endpoints with names, summaries, and problem responses"
```

---

### Task 4: Metadata for the Catalog module

**Files:**
- Modify: `src/PriceNegotiationApp.Modules.Catalog/Features/Products/List.cs`
- Modify: `src/PriceNegotiationApp.Modules.Catalog/Features/Products/Get.cs`
- Modify: `src/PriceNegotiationApp.Modules.Catalog/Features/Products/Create.cs`
- Modify: `src/PriceNegotiationApp.Modules.Catalog/Features/Products/Update.cs`
- Modify: `src/PriceNegotiationApp.Modules.Catalog/Features/Products/Delete.cs`

**Interfaces:**
- Produces: route names `ListProducts`, `GetProductById` (pre-existing — `CreatedAtRoute` in Create depends on it, do not rename), `CreateProduct`, `UpdateProduct`, `DeleteProduct`.

- [ ] **Step 1: Rewrite the five endpoint files**

Replace the full content of `src/PriceNegotiationApp.Modules.Catalog/Features/Products/List.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

internal static class List
{
    internal static void MapList(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (ListProductsHandler handler, CancellationToken ct,
                string? search = null, decimal? minPrice = null, decimal? maxPrice = null,
                string? sortBy = null, bool sortDesc = false, int page = 1, int pageSize = 20) =>
                TypedResults.Ok(await handler.HandleAsync(
                    new ProductQuery(search, minPrice, maxPrice, sortBy, sortDesc, page, pageSize), ct)))
            .CacheOutput(Policies.ShortCachePolicy)
            .AllowAnonymous()
            .WithName("ListProducts")
            .WithSummary("Search and page through products");
    }
}
```

Note: the original file imported `Microsoft.Extensions.DependencyInjection`; it is unused once rewritten — drop it.

Replace the full content of `src/PriceNegotiationApp.Modules.Catalog/Features/Products/Get.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

internal static class Get
{
    internal static void MapGetOne(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", async (Guid id, GetProductHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(id, ct)))
            .WithName("GetProductById")
            .WithSummary("Fetch a single product by id")
            .CacheOutput(Policies.ShortCachePolicy)
            .AllowAnonymous()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
```

Replace the full content of `src/PriceNegotiationApp.Modules.Catalog/Features/Products/Create.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

internal static class Create
{
    internal static void MapCreate(this RouteGroupBuilder group)
    {
        group.MapPost("/", async (CreateProductRequest request, CreateProductHandler handler,
                CancellationToken ct) =>
            {
                var response = await handler.HandleAsync(request, ct);
                return TypedResults.CreatedAtRoute(response, "GetProductById", new { id = response.Id });
            })
        .RequireRoles(UserRoles.Admin, UserRoles.Staff)
        .WithName("CreateProduct")
        .WithSummary("Create a new catalogue product")
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }
}
```

Replace the full content of `src/PriceNegotiationApp.Modules.Catalog/Features/Products/Update.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

internal static class Update
{
    internal static void MapUpdate(this RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", async (Guid id, UpdateProductRequest request,
                UpdateProductHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(id, request, ct)))
        .RequireRoles(UserRoles.Admin, UserRoles.Staff)
        .WithName("UpdateProduct")
        .WithSummary("Update an existing product")
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }
}
```

Replace the full content of `src/PriceNegotiationApp.Modules.Catalog/Features/Products/Delete.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

internal static class Delete
{
    internal static void MapDelete(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", async (Guid id, DeleteProductHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(id, ct);
            return TypedResults.NoContent();
        })
        .RequireRoles(UserRoles.Admin)
        .WithName("DeleteProduct")
        .WithSummary("Delete a product")
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
```

- [ ] **Step 2: Run relevant validation**

```bash
dotnet build
dotnet test tests/PriceNegotiationApp.IntegrationTests --filter "FullyQualifiedName~ProductsShould"
```

- [ ] **Step 3: Commit**

```bash
git add src/PriceNegotiationApp.Modules.Catalog/Features/Products
git commit -m "feat(catalog): document product endpoints with names, summaries, and problem responses"
```

---

### Task 5: Metadata for the Negotiations module

**Files:**
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Create.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/ListMine.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/List.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Get.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/CounterPropose.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Accept.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/RejectCurrentOffer.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Withdraw.cs`

**Interfaces:**
- Produces: route names `CreateNegotiation`, `ListMyNegotiations`, `ListNegotiations`, `GetNegotiationById`, `CounterProposeOffer`, `AcceptNegotiation`, `RejectCurrentOffer`, `WithdrawNegotiation`.

- [ ] **Step 1: Rewrite the eight endpoint files**

Replace the full content of `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Create.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
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
        .RequireRoles(UserRoles.Customer)
        .WithName("CreateNegotiation")
        .WithSummary("Start a price negotiation for a product")
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }
}
```

Replace the full content of `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/ListMine.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.SharedKernel;
using System.Security.Claims;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal static class ListMine
{
    internal static void MapListMine(this RouteGroupBuilder group)
    {
        group.MapGet("/mine", async (ClaimsPrincipal principal, ListMyNegotiationsHandler handler,
                CancellationToken ct, int page = 1, int pageSize = 20) =>
            TypedResults.Ok(await handler.HandleAsync(
                new PageQuery(page, pageSize), principal.ToCallerContext(), ct)))
        .RequireRoles(UserRoles.Customer)
        .WithName("ListMyNegotiations")
        .WithSummary("Page through the caller's own negotiations");
    }
}
```

Replace the full content of `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/List.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal static class List
{
    internal static void MapList(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (ListNegotiationsHandler handler, CancellationToken ct,
                int page = 1, int pageSize = 20) =>
            TypedResults.Ok(await handler.HandleAsync(new PageQuery(page, pageSize), ct)))
        .RequireRoles(UserRoles.Admin, UserRoles.Staff)
        .WithName("ListNegotiations")
        .WithSummary("Page through every negotiation");
    }
}
```

Replace the full content of `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Get.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
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
        .WithName("GetNegotiationById")
        .WithSummary("Fetch one negotiation the caller may access")
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
```

403 here is an ownership rule enforced inside `GetNegotiationHandler` (not visible from role metadata), so it is explicitly documented.

Replace the full content of `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/CounterPropose.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.SharedKernel;
using System.Security.Claims;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal static class CounterPropose
{
    internal static void MapCounterPropose(this RouteGroupBuilder group)
    {
        group.MapPatch("/{id:guid}/proposals", async (Guid id, CounterProposalRequest request,
                ClaimsPrincipal principal, CounterProposeHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(id, request, principal.ToCallerContext(), ct)))
        .WithName("CounterProposeOffer")
        .WithSummary("Counter the staff's current offer")
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }
}
```

409 covers both `negotiation_closed` and `no_proposals_remaining`; 422 covers `proposal_exceeds_limit`.

Replace the full content of `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Accept.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal static class Accept
{
    internal static void MapAccept(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/accept", async (Guid id, AcceptHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(id, ct)))
        .RequireRoles(UserRoles.Admin, UserRoles.Staff)
        .WithName("AcceptNegotiation")
        .WithSummary("Accept the customer's latest proposal")
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
```

Replace the full content of `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/RejectCurrentOffer.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal static class RejectCurrentOffer
{
    internal static void MapRejectCurrentOffer(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/decline", async (Guid id, RejectCurrentOfferHandler handler,
                CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(id, ct)))
        .RequireRoles(UserRoles.Admin, UserRoles.Staff)
        .WithName("RejectCurrentOffer")
        .WithSummary("Reject the customer's latest proposal")
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
```

Replace the full content of `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Withdraw.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.SharedKernel;
using System.Security.Claims;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal static class Withdraw
{
    internal static void MapWithdraw(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal principal,
                WithdrawHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(id, principal.ToCallerContext(), ct);
            return TypedResults.NoContent();
        })
        .WithName("WithdrawNegotiation")
        .WithSummary("Withdraw the negotiation (owner) or delete it (admin)")
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
```

- [ ] **Step 2: Run relevant validation**

```bash
dotnet build
dotnet test tests/PriceNegotiationApp.IntegrationTests --filter "FullyQualifiedName~NegotiationsShould"
dotnet test tests/PriceNegotiationApp.IntegrationTests --filter "FullyQualifiedName~ConcurrencyShould"
```

- [ ] **Step 3: Commit**

```bash
git add src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations
git commit -m "feat(negotiations): document negotiation endpoints with names, summaries, and problem responses"
```

---

### Task 6: Exclude infrastructure endpoints from the API description; enable OpenAPI in Testing

**Files:**
- Modify: `src/PriceNegotiationApp.Api/Extensions/PipelineExtensions.cs`

**Interfaces:**
- Produces: `GET /openapi/v1.json` served in the `Testing` environment (consumed by Task 7's test); health/jwks absent from the generated document.

- [ ] **Step 1: Update the pipeline wiring**

Three edits in `PipelineExtensions.UsePipeline`.

First, widen the OpenAPI gate so integration tests can assert against the real generated document (Scalar UI stays development-only):

```csharp
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapOpenApi();
    if (app.Environment.IsDevelopment())
    {
        app.MapScalarApiReference();
    }
}
```

Second, exclude the JWKS endpoint:

```csharp
app.MapGet("/.well-known/jwks.json", (EcSigningKey signingKey) => TypedResults.Json(
        new JwksResponse([new JwkKey(
            signingKey.PublicJwk.Kty,
            signingKey.PublicJwk.Crv,
            signingKey.PublicJwk.X,
            signingKey.PublicJwk.Y,
            signingKey.Kid)])))
    .AllowAnonymous()
    .ExcludeFromDescription();
```

Third, exclude both health probes:

```csharp
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") })
    .ExcludeFromDescription();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready"),
    ResponseWriter = ReadyHealthReport.WriteAsync,
})
.ExcludeFromDescription();
```

- [ ] **Step 2: Run relevant validation**

```bash
dotnet build
dotnet test tests/PriceNegotiationApp.IntegrationTests --filter "FullyQualifiedName~JwksShould"
dotnet test tests/PriceNegotiationApp.IntegrationTests --filter "FullyQualifiedName~ReadyHealth"
```

Behavioral endpoints unchanged; exclusion is description-only.

- [ ] **Step 3: Commit**

```bash
git add src/PriceNegotiationApp.Api/Extensions/PipelineExtensions.cs
git commit -m "feat(api): hide infrastructure endpoints from api description, serve openapi in testing"
```

---

### Task 7: OpenAPI contract smoke test + full validation

**Files:**
- Create: `tests/PriceNegotiationApp.IntegrationTests/OpenApiContractShould.cs`

**Interfaces:**
- Consumes: route names and problem statuses from Tasks 3–5; exclusions from Task 6; `IntegrationTestFixture.Anonymous` client.

- [ ] **Step 1: Add the contract test**

Create `tests/PriceNegotiationApp.IntegrationTests/OpenApiContractShould.cs`:

```csharp
using PriceNegotiationApp.IntegrationTests.Support;
using Shouldly;
using System.Net.Http.Json;
using Xunit;

namespace PriceNegotiationApp.IntegrationTests;

[Collection(ApiCollection.Name)]
public class OpenApiContractShould(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task Document_exposes_names_summaries_and_problem_responses()
    {
        var response = await fixture.Anonymous.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var document = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>(
            TestContext.Current.CancellationToken);
        var root = document!.RootElement;

        root.GetProperty("paths").TryGetProperty("/health/live", out _).ShouldBeFalse();
        root.GetProperty("paths").TryGetProperty("/.well-known/jwks.json", out _).ShouldBeFalse();

        var login = root.GetProperty("paths").GetProperty("/api/v1/auth/login").GetProperty("post");
        login.GetProperty("operationId").GetString().ShouldBe("Login");
        login.GetProperty("summary").GetString().ShouldNotBeNull();
        login.GetProperty("responses").TryGetProperty("401", out _).ShouldBeTrue();
        login.GetProperty("responses").TryGetProperty("429", out _).ShouldBeTrue();

        var counterPropose = root.GetProperty("paths")
            .GetProperty("/api/v1/negotiations/{id}/proposals").GetProperty("patch");
        counterPropose.GetProperty("responses").TryGetProperty("409", out _).ShouldBeTrue();
        counterPropose.GetProperty("responses").TryGetProperty("422", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Every_business_operation_has_a_stable_operation_id_and_summary()
    {
        var response = await fixture.Anonymous.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var document = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>(
            TestContext.Current.CancellationToken);
        var root = document!.RootElement.GetProperty("paths");

        var incomplete = new List<string>();
        foreach (var path in root.EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                if (operation.Value.ValueKind != System.Text.Json.JsonValueKind.Object)
                {
                    continue;
                }

                if (!operation.Value.TryGetProperty("operationId", out _) ||
                    !operation.Value.TryGetProperty("summary", out _))
                {
                    incomplete.Add($"{operation.Name.ToUpperInvariant()} {path.Name}");
                }
            }
        }

        incomplete.ShouldBeEmpty();
    }
}
```

- [ ] **Step 2: Run the new test**

```bash
dotnet test tests/PriceNegotiationApp.IntegrationTests --filter "FullyQualifiedName~OpenApiContractShould"
```

If `operationId` assertions fail because a framework strategy overrides `WithName` (not expected on net10.0 — official docs guarantee `WithName` becomes the operation ID), stop and investigate rather than loosening the assertion.

- [ ] **Step 3: Run the full gate**

```bash
dotnet format --verify-no-changes
dotnet build
dotnet test
```

All suites (unit, architecture, integration) must pass. Architecture tests pin repository patterns — expect them green since no persistence code changed.

- [ ] **Step 4: Commit**

```bash
git add tests/PriceNegotiationApp.IntegrationTests/OpenApiContractShould.cs
git commit -m "test(integration): lock openapi contract surface - names, summaries, problem responses"
```

---

## Completion Checklist

- [ ] All 16 business endpoints: group-authenticated by default, named, summarized, error-documented.
- [ ] Public surface frozen by `PublicSurfaceShould` (12 protected routes challenge anonymously).
- [ ] CORS actually enforced for configured origins; cache cannot leak cross-origin headers.
- [ ] `/health/*` and `/.well-known/jwks.json` absent from `/openapi/v1.json`.
- [ ] Full `dotnet test` green including architecture tests.
