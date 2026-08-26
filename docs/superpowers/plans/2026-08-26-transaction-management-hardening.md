# Transaction Management Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make concurrency conflicts return 409 instead of 500, make the create-negotiation flow one atomic commit, and mechanically pin who may commit — per the approved spec `docs/superpowers/specs/2026-08-26-transaction-management-review-design.md`.

**Architecture:** All fixes ride existing seams: the global exception mapper already owns status translation, handlers already own their single `SaveChangesAsync`, and ArchUnit-style doctrine tests already pin architectural laws. No MediatR, no pipeline behavior, no schema changes.

**Tech Stack:** .NET 10 / EF Core 10 + Npgsql (xmin system-column tokens already configured), xUnit v3 + Shouldly, Testcontainers PostgreSQL, existing `WebApplicationFactory` fixture.

## Global Constraints

- `TreatWarningsAsErrors=true`; zero warnings allowed.
- Endpoints stay transport-only; handlers own persistence (existing ArchUnit laws must keep passing).
- Stable machine-readable `code` extension on every ProblemDetails response.
- Module domain/persistence types are `internal` — integration tests must go through HTTP or EF Core metadata APIs (`EF.Property`, `Entry().Property()`), never internals.
- Integration tests need Docker (Testcontainers); plain unit facts live in the same projects without `[Collection]`.
- Full gate at the end: `dotnet format --verify-no-changes`, Release build, whole solution green.

---

### Task 1: Map concurrency conflicts to 409 (spec F-G1, part 1)

**Files:**
- Modify: `src/PriceNegotiationApp.SharedKernel/ErrorCodes.cs:7`
- Modify: `src/PriceNegotiationApp.Api/GlobalExceptionHandler.cs:30-50`
- Create: `tests/PriceNegotiationApp.IntegrationTests/GlobalExceptionHandlerShould.cs`

**Interfaces:**
- Produces: every `DbUpdateConcurrencyException` escaping a handler becomes HTTP 409, title "Resource changed meanwhile", `code = "concurrency_conflict"` (constant `ErrorCodes.ConcurrencyConflict`, renamed value — it currently reads `"conflict"` and has zero usages).

- [ ] **Step 1: Rename the error-code value**

In `src/PriceNegotiationApp.SharedKernel/ErrorCodes.cs` change:

```csharp
    public const string ConcurrencyConflict = "conflict";
```

to:

```csharp
    public const string ConcurrencyConflict = "concurrency_conflict";
```

- [ ] **Step 2: Add the mapper case**

In `GlobalExceptionHandler.TryHandleAsync`, add `using Microsoft.EntityFrameworkCore;` and insert this arm into the switch immediately above the `NotFoundException` line:

```csharp
            // 409 — another writer committed this aggregate first (xmin token fired)
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Resource changed meanwhile", ErrorCodes.ConcurrencyConflict),
```

- [ ] **Step 3: Unit-test the mapping**

Create `tests/PriceNegotiationApp.IntegrationTests/GlobalExceptionHandlerShould.cs`:

```csharp
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using PriceNegotiationApp.Api;
using Shouldly;
using System.Text.Json;
using Xunit;

namespace PriceNegotiationApp.IntegrationTests;

// Plain unit facts over the exception mapper; no Docker container required.
public class GlobalExceptionHandlerShould
{
    [Fact]
    public async Task Map_concurrency_conflicts_to_409_with_stable_code()
    {
        var (status, code) = await HandleAsync(new DbUpdateConcurrencyException("xmin race"));

        status.ShouldBe(StatusCodes.Status409Conflict);
        code.ShouldBe("concurrency_conflict");
    }

    [Fact]
    public async Task Keep_unknown_exceptions_on_the_internal_error_fallback()
    {
        var (status, code) = await HandleAsync(new InvalidOperationException("boom"));

        status.ShouldBe(StatusCodes.Status500InternalServerError);
        code.ShouldBe("internal_error");
    }

    private static async Task<(int Status, string Code)> HandleAsync(Exception exception)
    {
        var services = new ServiceCollection().AddProblemDetails().BuildServiceProvider();
        var sut = new GlobalExceptionHandler(
            services.GetRequiredService<IProblemDetailsService>(),
            new TestEnvironment(),
            NullLogger<GlobalExceptionHandler>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await sut.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);
        return (context.Response.StatusCode, document.RootElement.GetProperty("code").GetString()!);
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public string EnvironmentName { get; set; } = "Testing";
    }
}
```

- [ ] **Step 4: Run validation**

```bash
dotnet test --project tests/PriceNegotiationApp.IntegrationTests --filter GlobalExceptionHandler
```

- [ ] **Step 5: Commit**

```bash
git add src/PriceNegotiationApp.SharedKernel/ErrorCodes.cs src/PriceNegotiationApp.Api/GlobalExceptionHandler.cs tests/PriceNegotiationApp.IntegrationTests/GlobalExceptionHandlerShould.cs
git commit -m "fix(api): map concurrency conflicts to 409 problem details"
```

---

### Task 2: Prove the xmin token fires end-to-end (spec F-G1, part 2)

Module internals are invisible to the integration-test assembly, so both writers mutate their tracked entity through the EF metadata API (`Entry().Property(...).CurrentValue`) — provider-typed values, no domain calls needed.

**Files:**
- Create: `tests/PriceNegotiationApp.IntegrationTests/ConcurrencyShould.cs`

**Interfaces:**
- Consumes: `IntegrationTestFixture.Factory.Services` (root `IServiceProvider` of the hosted app, for creating real configured scopes), HTTP endpoints `/api/v1/products`, `/api/v1/negotiations`.
- Produces: regression proof that two concurrent `SaveChangesAsync` calls on one negotiation yield `DbUpdateConcurrencyException` from the loser.

- [ ] **Step 1: Write the race test**

Create `tests/PriceNegotiationApp.IntegrationTests/ConcurrencyShould.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PriceNegotiationApp.IntegrationTests.Support;
using PriceNegotiationApp.TestKit;
using Shouldly;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PriceNegotiationApp.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ConcurrencyShould(IntegrationTestFixture fixture)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Second_writer_of_one_negotiation_gets_a_concurrency_exception()
    {
        var negotiationId = await OpenNegotiationAsync();

        await using var scope1 = fixture.Factory.Services.CreateScope();
        await using var scope2 = fixture.Factory.Services.CreateScope();
        var db1 = scope1.ServiceProvider.GetRequiredService<Modules.Negotiations.Persistence.NegotiationsDbContext>();
        var db2 = scope2.ServiceProvider.GetRequiredService<Modules.Negotiations.Persistence.NegotiationsDbContext>();

        var first = await db1.Negotiations.SingleAsync(
            n => EF.Property<Guid>(n, "Id") == negotiationId, TestContext.Current.CancellationToken);
        var second = await db2.Negotiations.SingleAsync(
            n => EF.Property<Guid>(n, "Id") == negotiationId, TestContext.Current.CancellationToken);

        // Both writers loaded the same row; each mutates its tracked copy.
        db1.Entry(first).Property("CurrentOffer").CurrentValue = 70m;
        db2.Entry(second).Property("CurrentOffer").CurrentValue = 71m;

        await db1.SaveChangesAsync(TestContext.Current.CancellationToken);

        Should.Throw<DbUpdateConcurrencyException>(
            () => db2.SaveChangesAsync(TestContext.Current.CancellationToken));

        // The winner's state survives untouched.
        await using var verify = fixture.Factory.Services.CreateScope();
        var stored = await verify.ServiceProvider
            .GetRequiredService<Modules.Negotiations.Persistence.NegotiationsDbContext>()
            .Negotiations.AsNoTracking()
            .SingleAsync(n => EF.Property<Guid>(n, "Id") == negotiationId, TestContext.Current.CancellationToken);
        verify.ServiceProvider.GetRequiredService<Modules.Negotiations.Persistence.NegotiationsDbContext>()
            .Entry(stored).Property("CurrentOffer").CurrentValue.ShouldBe(70m);
    }

    private async Task<Guid> OpenNegotiationAsync()
    {
        var staff = await fixture.LoginAsStaffAsync();
        var createProduct = await staff.Client.PostAsJsonAsync("/api/v1/products",
            new { name = Fuzz.NewFaker().ProductName(), price = 100m }, TestContext.Current.CancellationToken);
        createProduct.StatusCode.ShouldBe(HttpStatusCode.Created);
        var product = await createProduct.Content.ReadFromJsonAsync<ProductResponse>(Json, TestContext.Current.CancellationToken);

        var customer = await fixture.CreateUserAsync();
        var open = await customer.Client.PostAsJsonAsync("/api/v1/negotiations",
            new { productId = product!.Id, proposedPrice = 80m }, TestContext.Current.CancellationToken);
        open.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await open.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return created.GetProperty("id").GetGuid();
    }
}
```

Note: `NegotiationsDbContext` is `internal`; the fully-qualified references compile because modules grant `InternalsVisibleTo` only to the composition root and their own test projects — if the compiler rejects them from this assembly, fall back to resolving the context through the interface-free route used here but declared as follows: extract `var dbType = Type.GetType("PriceNegotiationApp.Modules.Negotiations.Persistence.NegotiationsDbContext, PriceNegotiationApp.Modules.Negotiations")!` and resolve via `scope.ServiceProvider.GetRequiredService(dbType)` casting to `DbContext` (add `using Microsoft.EntityFrameworkCore;`). Prefer the direct generic form; use reflection only if IVT denies access.

- [ ] **Step 2: Run validation**

```bash
dotnet test --project tests/PriceNegotiationApp.IntegrationTests --filter ConcurrencyShould
```

- [ ] **Step 3: Commit**

```bash
git add tests/PriceNegotiationApp.IntegrationTests/ConcurrencyShould.cs
git commit -m "test(integration): prove xmin token rejects concurrent negotiation writers"
```

---

### Task 3: One atomic commit for create-negotiation (spec F-G2)

**Files:**
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/CreateNegotiationHandler.cs:14-38`

**Interfaces:**
- Consumes: unchanged `NegotiationAccess.GetOrCreateCustomerIdAsync` (its internal save joins the surrounding transaction), unchanged `DbWriteGuard.SaveOrConflictAsync`.

- [ ] **Step 1: Wrap provisioning + insert in one explicit transaction**

Replace the body of `HandleAsync` with:

```csharp
        var snapshot = await products.GetAsync(command.ProductId, ct)
                       ?? throw new NotFoundException("Product", command.ProductId);

        if (await NegotiationAccess.FindOpenAsync(db, snapshot.ProductId, caller.UserId, ct) is not null)
        {
            throw new ConflictException(NegotiationErrorCodes.NegotiationAlreadyOpen,
                "An open negotiation already exists for this product.");
        }

        // Provisioning the customer row and inserting the negotiation commit together:
        // a failed insert must not strand a permanent customer row (one commit point).
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var customerId = await NegotiationAccess.GetOrCreateCustomerIdAsync(db, caller.UserId, ct);
        var negotiation = Negotiation.Start(customerId, snapshot.ProductId, snapshot.Price,
            command.ProposedPrice, clock.GetUtcNow(), policy);
        await db.Negotiations.AddAsync(negotiation, ct);

        // The partial unique index is the real guard; a race that slipped past the
        // pre-check above surfaces here as a 409 instead of a 500.
        await db.SaveOrConflictAsync(
            _ => new ConflictException(NegotiationErrorCodes.NegotiationAlreadyOpen,
                "An open negotiation already exists for this product."), ct);
        await tx.CommitAsync(ct);

        return NegotiationResponses.ToResponse(negotiation);
```

- [ ] **Step 2: Run validation**

```bash
dotnet test --project tests/PriceNegotiationApp.IntegrationTests --filter FullyQualifiedName~NegotiationsShould|FullyQualifiedName~ConcurrencyShould
```

Creation, conflict, lifecycle, and the new race test all exercise this path.

- [ ] **Step 3: Commit**

```bash
git add src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/CreateNegotiationHandler.cs
git commit -m "fix(negotiations): make create flow one atomic commit"
```

---

### Task 4: Pin who may commit — architecture test (spec F-G3)

A source-level doctrine test in the existing architecture-tests project: direct invocations of `SaveChangesAsync` under `src/` are allowed only in `*Handler.cs`, `*SeedingHostedService.cs`, `DbWriteGuard.cs`, and `NegotiationAccess.cs` (the provisioning save inside the create-flow transaction). Deliberately simple and false-positive-free where an ArchUnit method-call graph would be fragile; matches the precedent of `Repository_ceremony_stays_out_of_the_codebase`.

**Files:**
- Modify: `tests/PriceNegotiationApp.ArchitectureTests/ArchitectureShould.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: build-time failure for any future stray commit point.

- [ ] **Step 1: Add the rule**

Append to `ArchitectureShould` (namespace usings already present; add none):

```csharp
    [Fact]
    public void Only_handlers_seeding_and_the_write_guard_commit_the_unit_of_work()
    {
        // F-05 doctrine, enforcement side: the single commit point lives in the
        // owning handler (or the seeding services / write guard / provisioning
        // helper inside the create-flow transaction). Nothing else may flush.
        var suffixAllowList = new[] { "Handler.cs", "SeedingHostedService.cs" };
        var nameAllowList = new[] { "DbWriteGuard.cs", "NegotiationAccess.cs" };

        var offenders = Directory
            .EnumerateFiles(Path.Combine(FindRepoRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                           && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(path => File.ReadAllText(path).Contains(".SaveChangesAsync(", StringComparison.Ordinal))
            .Where(path => !suffixAllowList.Any(path.EndsWith)
                           && !nameAllowList.Contains(Path.GetFileName(path)))
            .Select(path => Path.GetRelativePath(FindRepoRoot(), path))
            .ToList();

        offenders.ShouldBeEmpty(
            "SaveChangesAsync commits belong to feature handlers, seeding services, " +
            "DbWriteGuard, or NegotiationAccess provisioning");
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PriceNegotiationApp.slnx")))
        {
            directory = directory.Parent;
        }

        return directory!.FullName;
    }
```

(`Directory`, `File`, `Path`, `StringComparison`, `AppContext` come from `System.IO`/base class library via ImplicitUsings.)

- [ ] **Step 2: Run validation**

```bash
dotnet test --project tests/PriceNegotiationApp.ArchitectureTests
```

All rules including the new one must pass against the tree produced by Tasks 1–3.

- [ ] **Step 3: Commit**

```bash
git add tests/PriceNegotiationApp.ArchitectureTests/ArchitectureShould.cs
git commit -m "test(architecture): pin SaveChangesAsync callers to handlers, seeding, and the write guard"
```

---

### Task 5: Docs tell the truth about this repo (spec F-G4) + full gate

**Files:**
- Modify: `docs/transaction-management-patterns.md:627-634` ("Recommendation for this repo")
- Modify: `README.md:15` (Persistence stack-table row)

- [ ] **Step 1: Replace the transplanted recommendation**

Replace everything from `### Recommendation for this repo` to end of file with:

```markdown
### Recommendation for this repo (2026-08-26 audit)

This codebase implements Option 3 with Option 5 deliberately deferred:

- Three module-owned scoped `DbContext`s (Identity, Catalog, Negotiations); one commit
  point per use case, owned by that use case's `*Handler`; cross-module reads only via
  the `IProductPriceProvider` port.
- Client-generated GUIDv7 keys everywhere, so flows fit one flush; the single
  multi-save flow (`CreateNegotiationHandler`) wraps provisioning + insert in one
  explicit transaction (Case B above).
- `xmin` optimistic tokens sit on both write aggregates; conflicts surface as
  `DbUpdateConcurrencyException` mapped to HTTP 409 (`concurrency_conflict`).
- Unique-index races translate to 409 through `DbWriteGuard.SaveOrConflictAsync`.
- No MediatR pipeline behavior (Option 4) by design: handlers own persistence, and the
  architecture test `Only_handlers_seeding_and_the_write_guard_commit_the_unit_of_work`
  pins who may call `SaveChangesAsync`.
- Outbox/events arrive with the first real subscriber (ddd-audit spec §F-04); today's
  cross-module edge is a synchronous read, not a workflow.
```

- [ ] **Step 2: Update the README claim**

Change the Persistence row in the Stack table:

```markdown
| Persistence | EF Core 10 + Npgsql (PostgreSQL 17), snake_case schema, xmin concurrency (conflicts surface as 409) |
```

- [ ] **Step 3: Run the full gate**

```bash
dotnet format --verify-no-changes
dotnet build -c Release
dotnet test --solution PriceNegotiationApp.slnx -c Release
```

All three clean.

- [ ] **Step 4: Commit**

```bash
git add docs/transaction-management-patterns.md README.md
git commit -m "docs: reconcile transaction pattern guidance with this codebase"
```
