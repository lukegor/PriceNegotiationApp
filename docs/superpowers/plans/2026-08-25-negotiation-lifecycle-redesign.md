# Services & Business Logic Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the five audited defects in the Negotiations lifecycle (ambiguous decline semantics, destructive withdrawal, live-evaluated policy, uniqueness races surfacing as HTTP 500, read-path inconsistencies) per `docs/superpowers/specs/2026-08-25-services-application-business-logic-audit-design.md`.

**Architecture:** Keep the modular-monolith vertical-slice design unchanged. Rework the `Negotiation` aggregate into an explicit state machine (`Open | Accepted | Rejected | Withdrawn`) that snapshots its policy limits at creation, translate PostgreSQL unique violations into HTTP 409 via one SharedKernel guard, make owner DELETE a soft close while Admin DELETE stays a hard delete, and clean up read paths.

**Tech Stack:** .NET 10 / C# latest, ASP.NET Core minimal APIs, EF Core 10 + Npgsql (PostgreSQL 17), Vogen value objects, xUnit v3 + Shouldly + Bogus, Testcontainers.

## Global Constraints

- Source spec: `docs/superpowers/specs/2026-08-25-services-application-business-logic-audit-design.md`.
- `net10.0`, `Nullable=enable`, `TreatWarningsAsErrors=true`, `CodeAnalysisTreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true` (from `Directory.Build.props`) — any warning fails the build.
- Packages centrally managed in `Directory.Packages.props`; never put versions in a `.csproj`.
- Module implementation types are `internal`, visible only to `Api` and each module's own test project; integration tests exercise HTTP only.
- Error contract: RFC 7807 ProblemDetails plus machine-readable `code`; stable codes: `negotiation_closed`, `proposal_exceeds_limit`, `negotiation_already_open`, `no_proposals_remaining`, `email_already_registered`.
- API routes never change (staff decline stays `POST /api/v1/negotiations/{id}/decline`; owner withdraw stays `DELETE /api/v1/negotiations/{id}`).
- Stored status ints stay stable: legacy `Declined = 3` becomes `Rejected = 3` by rename only (no data remap); add `Withdrawn = 4`. `Open = 1` unchanged so the partial unique index filter (`status = 1`) is untouched.
- Every commit builds with zero warnings and keeps touched projects' tests green.
- All commands run from repo root; shell is pwsh.
- Integration tests require a running Docker daemon (Testcontainers boots postgres:17-alpine). If Docker is unavailable, state that plainly instead of pretending they ran.

---

### Task 1: Unique-violation translation guard (SharedKernel)

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `src/PriceNegotiationApp.SharedKernel/PriceNegotiationApp.SharedKernel.csproj`
- Create: `src/PriceNegotiationApp.SharedKernel/DbWriteGuard.cs`
- Test: `tests/PriceNegotiationApp.Modules.Negotiations.Tests/DbWriteGuardShould.cs`

**Interfaces:**
- Consumes: nothing new (`DbContext`, `DbUpdateException`, `Npgsql.PostgresException`).
- Produces (consumed by Tasks 2 and 3):
  - `bool DbWriteGuard.IsUniqueViolation(Exception exception, out string constraintName)` — walks inner exceptions; true iff a `PostgresException` with `SqlState == "23505"` exists; outputs its `ConstraintName ?? ""`.
  - `Task DbWriteGuard.SaveOrConflictAsync(this DbContext db, Func<string, Exception> conflictFactory, CancellationToken ct)` — saves; on unique violation throws `conflictFactory(constraintName)`.

- [ ] **Step 1: Pin the bare Npgsql package version**

Discover the exact transitive `Npgsql` version already flowing through the pinned EF provider:

```bash
dotnet list src/PriceNegotiationApp.Modules.Negotiations/PriceNegotiationApp.Modules.Negotiations.csproj package --include-transitive | Select-String "^   > Npgsql\."
```

Use the printed bare `Npgsql` version verbatim (do not guess). In `Directory.Packages.props`, inside the direct-references `<ItemGroup>`, keep alphabetical order — insert directly after the existing `Npgsql.EntityFrameworkCore.PostgreSQL` line:

```xml
<PackageVersion Include="Npgsql" Version="<EXACT_VERSION_FROM_COMMAND>" />
```

In `src/PriceNegotiationApp.SharedKernel/PriceNegotiationApp.SharedKernel.csproj`, inside the existing packages `<ItemGroup>`:

```xml
<PackageReference Include="Npgsql" />
```

- [ ] **Step 2: Implement DbWriteGuard**

Create `src/PriceNegotiationApp.SharedKernel/DbWriteGuard.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace PriceNegotiationApp.SharedKernel;

/// <summary>
/// Translates PostgreSQL uniqueness violations raised during SaveChanges into
 /// caller-supplied semantic exceptions, so check-then-insert races surface as
/// conflicts instead of HTTP 500.
/// </summary>
public static class DbWriteGuard
{
    public static bool IsUniqueViolation(Exception exception, out string constraintName)
    {
        constraintName = string.Empty;
        for (var current = (Exception?)exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres)
            {
                constraintName = postgres.ConstraintName ?? string.Empty;
                return true;
            }
        }

        return false;
    }

    public static async Task SaveOrConflictAsync(
        this DbContext db, Func<string, Exception> conflictFactory, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, out var constraint))
        {
            throw conflictFactory(constraint);
        }
    }
}
```

- [ ] **Step 3: Add unit tests**

Create `tests/PriceNegotiationApp.Modules.Negotiations.Tests/DbWriteGuardShould.cs` (this test project already references the Negotiations module, which transitively brings Npgsql):

```csharp
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PriceNegotiationApp.SharedKernel;
using Shouldly;
using Xunit;

namespace PriceNegotiationApp.Modules.Negotiations.Tests;

public class DbWriteGuardShould
{
    private const string ConstraintName = "uq_negotiations_open_product_customer";

    [Fact]
    public void Detect_unique_violation_wrapped_in_DbUpdateException()
    {
        var inner = new PostgresException("duplicate key value", "ERROR", "ERROR",
            PostgresErrorCodes.UniqueViolation, constraintName: ConstraintName);

        var found = DbWriteGuard.IsUniqueViolation(new DbUpdateException("save failed", inner),
            out var constraint);

        found.ShouldBeTrue();
        constraint.ShouldBe(ConstraintName);
    }

    [Fact]
    public void Ignore_other_postgres_error_codes()
    {
        var inner = new PostgresException("foreign key", "ERROR", "ERROR",
            PostgresErrorCodes.ForeignKeyViolation, constraintName: ConstraintName);

        DbWriteGuard.IsUniqueViolation(new DbUpdateException("save failed", inner), out _)
            .ShouldBeFalse();
    }

    [Fact]
    public void Ignore_unrelated_exception_types()
    {
        DbWriteGuard.IsUniqueViolation(new InvalidOperationException("nope"), out _)
            .ShouldBeFalse();
    }

    [Fact]
    public void SaveOrConflict_throws_factory_exception_with_constraint_name()
    {
        var inner = new PostgresException("duplicate key value", "ERROR", "ERROR",
            PostgresErrorCodes.UniqueViolation, constraintName: ConstraintName);
        var db = new ThrowingDbContext();

        var thrown = Should.Throw<ConflictException>(() =>
            db.SaveOrConflictAsync(
                constraint => new ConflictException($"hit:{constraint}", "conflict"),
                TestContext.Current.CancellationToken).GetAwaiter().GetResult());

        thrown.Code.ShouldBe($"hit:{ConstraintName}");
    }

    [Fact]
    public void SaveOrConflict_rerethrows_non_unique_failures()
    {
        var db = new ThrowingDbContext(withUniqueViolation: false);

        var thrown = Should.Throw<DbUpdateException>(() =>
            db.SaveOrConflictAsync(
                constraint => new ConflictException(constraint, "conflict"),
                TestContext.Current.CancellationToken).GetAwaiter().GetResult());

        thrown.InnerException.ShouldBeOfType<InvalidOperationException>();
    }

    private sealed class ThrowingDbContext(bool withUniqueViolation = true) : DbContext
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Exception inner = withUniqueViolation
                ? new PostgresException("duplicate key value", "ERROR", "ERROR",
                    PostgresErrorCodes.UniqueViolation, constraintName: ConstraintName)
                : new InvalidOperationException("boom");
            throw new DbUpdateException("save failed", inner);
        }
    }
}
```

Note: if the `PostgresException` constructor overload with the `constraintName:` named argument does not compile against the pinned Npgsql version, fall back to the four-argument constructor and assign the public `ConstraintName` property right after construction — semantics identical.

- [ ] **Step 4: Run validation**

```bash
dotnet build && dotnet test tests/PriceNegotiationApp.Modules.Negotiations.Tests
```

All six tests green, zero warnings.

- [ ] **Step 5: Commit**

```bash
git add Directory.Packages.props src/PriceNegotiationApp.SharedKernel/PriceNegotiationApp.SharedKernel.csproj src/PriceNegotiationApp.SharedKernel/DbWriteGuard.cs tests/PriceNegotiationApp.Modules.Negotiations.Tests/DbWriteGuardShould.cs
git commit -m "feat(shared): translate postgres unique violations into semantic conflicts"
```

---

### Task 2: Negotiation state machine, policy snapshot, race-safe writes

This task is one compile unit: the aggregate's public surface changes, so every Negotiations feature file and both test suites move together. Do not commit halfway through.

**Files:**
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Domain/NegotiationStatus.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Domain/Negotiation.cs` (full rewrite)
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Persistence/Configurations/NegotiationConfiguration.cs`
- Create: `src/PriceNegotiationApp.Modules.Negotiations/Persistence/Migrations/<timestamp>_SnapshotPolicyLimitsAndWithdrawn.cs` (+ Designer + snapshot, generated)
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/NegotiationModels.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/NegotiationAccess.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Create.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/CounterPropose.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Accept.cs`
- Delete + recreate: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Decline.cs` → `RejectCurrentOffer.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/Withdraw.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/NegotiationEndpoints.cs`
- Test: `tests/PriceNegotiationApp.Modules.Negotiations.Tests/NegotiationLifecycleShould.cs` (rewrite)
- Test: `tests/PriceNegotiationApp.IntegrationTests/NegotiationsShould.cs` (one expectation edit)

**Interfaces:**
- Consumes from Task 1: `DbWriteGuard.SaveOrConflictAsync`, `DbWriteGuard.IsUniqueViolation`.
- Produces:
  - `enum NegotiationStatus { Open = 1, Accepted = 2, Rejected = 3, Withdrawn = 4 }`
  - `static Negotiation Negotiation.Start(CustomerId customerId, Guid productId, decimal basePriceSnapshot, decimal initialOffer, DateTimeOffset now, INegotiationPolicy policy)`
  - `NegotiationOutcome Negotiation.CounterPropose(decimal offer, DateTimeOffset now)` — no policy parameter
  - `void Negotiation.Accept(DateTimeOffset now)`
  - `void Negotiation.RejectCurrentOffer(DateTimeOffset now)` — stays Open, stamps LastStaffActionAtUtc
  - `void Negotiation.Withdraw(DateTimeOffset now)` — terminal Withdrawn, sets DecidedAtUtc
  - `int Negotiation.RemainingProposals()` — no policy parameter
  - New aggregate properties: `int MaxProposals`, `decimal OfferMultiplierLimit`, `DateTimeOffset? LastStaffActionAtUtc`
  - `static NegotiationResponse NegotiationResponses.ToResponse(Negotiation n)` — single parameter
  - `record StaffActionResponse(string Outcome, NegotiationResponse Negotiation)` in NegotiationModels.cs
  - `static Task<Negotiation> NegotiationAccess.RequireReadOnlyAsync(NegotiationsDbContext db, Guid id, CancellationToken ct)` — AsNoTracking load

- [ ] **Step 1: Rework the status enum**

Replace the contents of `Domain/NegotiationStatus.cs`:

```csharp
namespace PriceNegotiationApp.Modules.Negotiations.Domain;

internal enum NegotiationStatus
{
    Open = 1,
    Accepted = 2,

    /// <summary>Terminal. Reached only via auto-rejection of an over-limit counter-proposal.</summary>
    Rejected = 3,

    /// <summary>Terminal. Owner withdrew; row and history are preserved.</summary>
    Withdrawn = 4,
}
```

The rename `Declined → Rejected` keeps ordinal 3, so existing rows need no data migration.

- [ ] **Step 2: Rewrite the aggregate**

Replace the contents of `Domain/Negotiation.cs`:

```csharp
namespace PriceNegotiationApp.Modules.Negotiations.Domain;

internal sealed class Negotiation
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

    /// <summary>Proposal budget snapshotted from the active policy at creation time.</summary>
    public int MaxProposals { get; private set; }

    /// <summary>Offer multiplier limit snapshotted from the active policy at creation time.</summary>
    public decimal OfferMultiplierLimit { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset LastProposalAtUtc { get; private set; }

    /// <summary>Most recent staff reject-current-offer action; does not change status.</summary>
    public DateTimeOffset? LastStaffActionAtUtc { get; private set; }

    public DateTimeOffset? DecidedAtUtc { get; private set; }

    public uint Version { get; private set; }

    private Negotiation()
    {
    }

    private Negotiation(
        NegotiationId id, Guid productId, CustomerId customerId, decimal basePrice, decimal initialOffer,
        INegotiationPolicy policy, DateTimeOffset createdAtUtc)
    {
        Id = id;
        ProductId = productId;
        CustomerId = customerId;
        BasePrice = basePrice;
        CurrentOffer = initialOffer;
        MaxProposals = policy.MaxProposalsPerNegotiation;
        OfferMultiplierLimit = policy.ProposalMultiplierLimit;
        Status = NegotiationStatus.Open;
        ProposalsUsed = 1;
        CreatedAtUtc = createdAtUtc;
        LastProposalAtUtc = createdAtUtc;
    }

    public static Negotiation Start(
        CustomerId customerId, Guid productId, decimal basePriceSnapshot, decimal initialOffer,
        DateTimeOffset now, INegotiationPolicy policy)
    {
        EnsureWithinLimit(basePriceSnapshot, initialOffer, policy.ProposalMultiplierLimit);
        return new Negotiation(NegotiationId.From(Guid.CreateVersion7()), productId, customerId,
            basePriceSnapshot, initialOffer, policy, now);
    }

    public NegotiationOutcome CounterPropose(decimal offer, DateTimeOffset now)
    {
        EnsureOpen();
        if (ProposalsUsed >= MaxProposals)
        {
            return NegotiationOutcome.NoProposalsRemaining;
        }

        try
        {
            EnsureWithinLimit(BasePrice, offer, OfferMultiplierLimit);
        }
        catch (ProposalExceedsLimitException)
        {
            Status = NegotiationStatus.Rejected;
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
    /// customer may spend a remaining proposal; the proposal budget is untouched.
    /// It terminates only via Accept, auto-rejection, or withdrawal.
    /// </summary>
    public void RejectCurrentOffer(DateTimeOffset now)
    {
        EnsureOpen();
        LastStaffActionAtUtc = now;
    }

    /// <summary>Owner abandons the negotiation; state becomes terminal, history is preserved.</summary>
    public void Withdraw(DateTimeOffset now) => Decide(NegotiationStatus.Withdrawn, now);

    public int RemainingProposals() => Math.Max(0, MaxProposals - ProposalsUsed);

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

    private static void EnsureWithinLimit(decimal basePrice, decimal offer, decimal multiplierLimit)
    {
        var limit = decimal.Round(basePrice * multiplierLimit, 2);
        Price.From(offer);
        if (offer > limit)
        {
            throw new ProposalExceedsLimitException(limit);
        }
    }
}
```

`INegotiationPolicy` is now consumed exactly once — at `Start`. Nothing else in the module may take it as a parameter after this task.

- [ ] **Step 3: Update persistence configuration and name the unique index**

In `Persistence/Configurations/NegotiationConfiguration.cs`, replace the `HasIndex` block (currently lines 22–24) and add the two new column mappings. The full method body becomes:

```csharp
public void Configure(EntityTypeBuilder<Negotiation> builder)
{
    builder.ToTable("negotiations");
    builder.HasKey(n => n.Id);
    builder.Property(n => n.Id).HasConversion(id => id.Value, value => NegotiationId.From(value))
        .ValueGeneratedNever();
    // Plain Guid key: product_id has NO FK by design (separate schemas/modules).
    // Existence is validated at creation; negotiations survive deletion on snapshots.
    builder.Property(n => n.CustomerId).HasConversion(id => id.Value, value => CustomerId.From(value));
    builder.Property(n => n.BasePrice).HasColumnType("numeric(18,2)");
    builder.Property(n => n.CurrentOffer).HasColumnType("numeric(18,2)");
    builder.Property(n => n.OfferMultiplierLimit).HasColumnType("numeric(5,2)");
    builder.Property(n => n.Status).HasConversion<int>();
    builder.HasOne<Customer>().WithMany().HasForeignKey(n => n.CustomerId).OnDelete(DeleteBehavior.Cascade);
    builder.HasIndex(n => new { n.ProductId, n.CustomerId })
        .HasDatabaseName("uq_negotiations_open_product_customer")
        .IsUnique()
        .HasFilter($"status = {(int)NegotiationStatus.Open}");
    builder.Property(n => n.Version).IsRowVersion();
}
```

(`max_proposals` and `last_staff_action_at_utc` need no explicit configuration — the snake_case naming convention handles them.)

- [ ] **Step 4: Generate and hand-finish the migration**

```bash
dotnet tool install --global dotnet-ef --version 10.* ; dotnet ef migrations add SnapshotPolicyLimitsAndWithdrawn --context NegotiationsDbContext -p src/PriceNegotiationApp.Modules.Negotiations -o Persistence/Migrations
```

(If the tool is already installed, skip the install part.) The generated `Up()` will add three columns without defaults — that fails on existing rows. Edit the generated migration's `Up()` so the two NOT NULL columns carry backfill defaults:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<int>(
        name: "max_proposals",
        table: "negotiations",
        type: "integer",
        nullable: false,
        defaultValue: 3);

    migrationBuilder.AddColumn<decimal>(
        name: "offer_multiplier_limit",
        table: "negotiations",
        type: "numeric(5,2)",
        nullable: false,
        defaultValue: 2.0m);

    migrationBuilder.AddColumn<DateTimeOffset>(
        name: "last_staff_action_at_utc",
        table: "negotiations",
        type: "timestamp with time zone",
        nullable: true);

    migrationBuilder.DropIndex(
        name: "<GENERATED_OLD_INDEX_NAME>", // e.g. ix_negotiations_product_id_customer_id — copy from the generated file
        table: "negotiations");

    migrationBuilder.CreateIndex(
        name: "uq_negotiations_open_product_customer",
        table: "negotiations",
        columns: new[] { "product_id", "customer_id" },
        unique: true,
        filter: "status = 1");
}
```

Keep whatever index names the generator actually produced — only inject the `defaultValue:` arguments; do not hand-write other operations. The `Down()` stays as generated.

- [ ] **Step 5: Update models and access helpers**

In `Features/Negotiations/NegotiationModels.cs`: delete the old `ToResponse`, add the staff-action record, final content of the mapper region:

```csharp
internal static class NegotiationResponses
{
    internal static NegotiationResponse ToResponse(Negotiation n) =>
        new(n.Id.Value, n.ProductId, n.BasePrice, n.CurrentOffer, n.Status.ToString(),
            n.ProposalsUsed, n.RemainingProposals(), n.CreatedAtUtc, n.LastProposalAtUtc, n.DecidedAtUtc);
}

internal sealed record StaffActionResponse(string Outcome, NegotiationResponse Negotiation);
```

Everything else in the file stays as-is.

In `Features/Negotiations/NegotiationAccess.cs`, add this method next to `RequireAsync`:

```csharp
public static async Task<Negotiation> RequireReadOnlyAsync(NegotiationsDbContext db, Guid id, CancellationToken ct) =>
    await db.Negotiations.AsNoTracking().FirstOrDefaultAsync(n => n.Id == NegotiationId.From(id), ct)
    ?? throw new NotFoundException(nameof(Negotiation), id);
```

Replace `GetOrCreateCustomerIdAsync` with a race-safe version (unique violation on `customers.identity_user_id` → refetch):

```csharp
public static async Task<CustomerId> GetOrCreateCustomerIdAsync(
    NegotiationsDbContext db, Guid identityUserId, CancellationToken ct)
{
    var existing = await CustomerByIdentityAsync(db, identityUserId, ct);
    if (existing is not null)
    {
        return existing.Id;
    }

    var customer = Customer.Create(identityUserId);
    db.Customers.Add(customer);
    try
    {
        await db.SaveChangesAsync(ct);
    }
    catch (DbUpdateException ex) when (DbWriteGuard.IsUniqueViolation(ex, out _))
    {
        db.Entry(customer).State = EntityState.Detached;
        return (await CustomerByIdentityAsync(db, identityUserId, ct))!.Id;
    }

    return customer.Id;
}
```

Add these usings to the file top: `Microsoft.EntityFrameworkCore` (already present), plus `PriceNegotiationApp.SharedKernel` (already present).

- [ ] **Step 6: Rewrite the feature endpoints**

`Features/Negotiations/Create.cs` — full file:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.Modules.Negotiations.Persistence;
using PriceNegotiationApp.Modules.Negotiations.Ports;
using PriceNegotiationApp.SharedKernel;
using System.Security.Claims;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal static class Create
{
    internal static void MapCreate(this RouteGroupBuilder group)
    {
        group.MapPost("/", async (CreateNegotiationRequest request, ClaimsPrincipal principal,
                NegotiationsDbContext db, IProductPriceProvider products, INegotiationPolicy policy,
                TimeProvider clock, CancellationToken ct) =>
            {
                var caller = principal.ToCallerContext();
                var snapshot = await products.GetAsync(request.ProductId, ct)
                               ?? throw new NotFoundException("Product", request.ProductId);

                if (await NegotiationAccess.FindOpenAsync(db, snapshot.ProductId, caller.UserId, ct) is not null)
                {
                    throw new ConflictException(NegotiationErrorCodes.NegotiationAlreadyOpen,
                        "An open negotiation already exists for this product.");
                }

                var customerId = await NegotiationAccess.GetOrCreateCustomerIdAsync(db, caller.UserId, ct);
                var negotiation = Negotiation.Start(customerId, snapshot.ProductId, snapshot.Price,
                    request.ProposedPrice, clock.GetUtcNow(), policy);
                await db.Negotiations.AddAsync(negotiation, ct);
                // The partial unique index is the real guard; a race that slipped past the
                // pre-check above surfaces here as a 409 instead of a 500.
                await db.SaveOrConflictAsync(
                    _ => new ConflictException(NegotiationErrorCodes.NegotiationAlreadyOpen,
                        "An open negotiation already exists for this product."), ct);
                return TypedResults.Created("/api/v1/negotiations/mine",
                    NegotiationResponses.ToResponse(negotiation));
            })
        .RequireRoles(UserRoles.Customer);
    }
}
```

`Features/Negotiations/CounterPropose.cs` — full file:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.Modules.Negotiations.Persistence;
using PriceNegotiationApp.SharedKernel;
using System.Security.Claims;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal static class CounterPropose
{
    internal static void MapCounterPropose(this RouteGroupBuilder group)
    {
        group.MapPatch("/{id:guid}/proposals", async (Guid id, CounterProposalRequest request,
                ClaimsPrincipal principal, NegotiationsDbContext db,
                TimeProvider clock, CancellationToken ct) =>
            {
                var caller = principal.ToCallerContext();
                var negotiation = await NegotiationAccess.RequireOwnedAsync(db, caller, id, ct);

                var outcome = negotiation.CounterPropose(request.ProposedPrice, clock.GetUtcNow());
                if (outcome == NegotiationOutcome.NoProposalsRemaining)
                {
                    throw new ConflictException(NegotiationErrorCodes.NoProposalsRemaining,
                        "No proposals remain for this negotiation.");
                }

                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(new CounterProposalOutcome(outcome.ToString(),
                    NegotiationResponses.ToResponse(negotiation)));
            })
        .RequireAuthorization();
    }
}
```

`Features/Negotiations/Accept.cs` — full file:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Negotiations.Persistence;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal static class Accept
{
    internal static void MapAccept(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/accept", async (Guid id, NegotiationsDbContext db,
                TimeProvider clock, CancellationToken ct) =>
            {
                var negotiation = await NegotiationAccess.RequireAsync(db, id, ct);
                negotiation.Accept(clock.GetUtcNow());
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(new StaffActionResponse("accepted",
                    NegotiationResponses.ToResponse(negotiation)));
            })
        .RequireRoles(UserRoles.Admin, UserRoles.Staff);
    }
}
```

Delete `Features/Negotiations/Decline.cs`; create `Features/Negotiations/RejectCurrentOffer.cs` (route unchanged):

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Negotiations.Persistence;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal static class RejectCurrentOffer
{
    internal static void MapRejectCurrentOffer(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/decline", async (Guid id, NegotiationsDbContext db,
                TimeProvider clock, CancellationToken ct) =>
            {
                var negotiation = await NegotiationAccess.RequireAsync(db, id, ct);
                negotiation.RejectCurrentOffer(clock.GetUtcNow());
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(new StaffActionResponse("current_offer_rejected",
                    NegotiationResponses.ToResponse(negotiation)));
            })
        .RequireRoles(UserRoles.Admin, UserRoles.Staff);
    }
}
```

`Features/Negotiations/Withdraw.cs` — full file (owner soft-close, admin hard delete):

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Negotiations.Persistence;
using PriceNegotiationApp.SharedKernel;
using System.Security.Claims;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal static class Withdraw
{
    internal static void MapWithdraw(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal principal, NegotiationsDbContext db,
                TimeProvider clock, CancellationToken ct) =>
            {
                var caller = principal.ToCallerContext();
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
                return TypedResults.NoContent();
            })
        .RequireAuthorization();
    }
}
```

`Features/Negotiations/Get.cs` — swap the load call to the read-only variant; line 21 becomes:

```csharp
var negotiation = await NegotiationAccess.RequireReadOnlyAsync(db, id, ct);
```

`Features/Negotiations/ListMine.cs` — short-circuit unknown customers to an empty page; replace lines 21–26 with:

```csharp
var customer = await NegotiationAccess.CustomerByIdentityAsync(db, caller.UserId, ct);
if (customer is null)
{
    return TypedResults.Ok(new PagedResult<NegotiationResponse>(
        [], query.SafePage, query.SafePageSize, 0));
}

var q = db.Negotiations.AsNoTracking().Where(n => n.CustomerId == customer.Id);
```

`NegotiationEndpoints.cs` — replace `group.MapDecline();` with `group.MapRejectCurrentOffer();`.

- [ ] **Step 7: Rewrite the domain unit tests**

Replace the full contents of `tests/PriceNegotiationApp.Modules.Negotiations.Tests/NegotiationLifecycleShould.cs`:

```csharp
using Bogus;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using Shouldly;
using Xunit;

namespace PriceNegotiationApp.Modules.Negotiations.Tests;

public class NegotiationLifecycleShould
{
    private static readonly DefaultNegotiationPolicy Policy = new();
    private readonly Faker _faker = new();
    private readonly Guid _productId = Guid.CreateVersion7();

    private const decimal BasePrice = 100m;
    private readonly DateTimeOffset _now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private Negotiation StartValid() =>
        Negotiation.Start(CustomerId.From(_faker.Random.Guid()), _productId, BasePrice, 80m, _now, Policy);

    [Fact]
    public void Start_records_initial_proposal_snapshots_policy_and_consumes_one_of_three_budgets()
    {
        var negotiation = StartValid();

        negotiation.Status.ShouldBe(NegotiationStatus.Open);
        negotiation.ProposalsUsed.ShouldBe(1);
        negotiation.MaxProposals.ShouldBe(3);
        negotiation.OfferMultiplierLimit.ShouldBe(2.0m);
        negotiation.BasePrice.ShouldBe(100m);
        negotiation.RemainingProposals().ShouldBe(2);
    }

    [Fact]
    public void Start_rejects_offer_over_twice_base_price() =>
        Should.Throw<ProposalExceedsLimitException>(
            () => Negotiation.Start(CustomerId.From(_faker.Random.Guid()), _productId, BasePrice, 201m, _now, Policy));

    [Fact]
    public void Start_accepts_offer_exactly_at_limit()
    {
        var negotiation = Negotiation.Start(CustomerId.From(_faker.Random.Guid()), _productId, BasePrice, 200m, _now, Policy);

        negotiation.CurrentOffer.ShouldBe(200m);
    }

    [Fact]
    public void CounterPropose_stores_new_offer_within_limit()
    {
        var negotiation = StartValid();

        var outcome = negotiation.CounterPropose(90m, _now.AddMinutes(5));

        outcome.ShouldBe(NegotiationOutcome.CounterProposed);
        negotiation.CurrentOffer.ShouldBe(90m);
        negotiation.ProposalsUsed.ShouldBe(2);
        negotiation.Status.ShouldBe(NegotiationStatus.Open);
    }

    [Fact]
    public void CounterPropose_over_limit_auto_rejects_and_closes()
    {
        var negotiation = StartValid();

        var outcome = negotiation.CounterPropose(500m, _now.AddMinutes(5));

        outcome.ShouldBe(NegotiationOutcome.AutoRejected);
        negotiation.Status.ShouldBe(NegotiationStatus.Rejected);
        negotiation.DecidedAtUtc.ShouldNotBeNull();
    }

    [Fact]
    public void CounterPropose_uses_limits_snapshotted_at_creation_not_current_config()
    {
        var generousPolicy = new StaticPolicy(maxProposals: 5, multiplierLimit: 3.0m);
        var negotiation = Negotiation.Start(
            CustomerId.From(_faker.Random.Guid()), _productId, BasePrice, 80m, _now, generousPolicy);

        // The DI container now hands out the default (stricter) policy; the aggregate
        // must still obey the rules it was created under.
        var outcome = negotiation.CounterPropose(250m, _now.AddMinutes(5));

        outcome.ShouldBe(NegotiationOutcome.CounterProposed); // legal under 3.0x, illegal under 2.0x
        negotiation.ProposalsUsed.ShouldBe(2);
        negotiation.RemainingProposals().ShouldBe(3);
    }

    [Fact]
    public void CounterPropose_after_budget_exhaustion_returns_NoProposalsRemaining()
    {
        var negotiation = StartValid();
        negotiation.CounterPropose(90m, _now);
        negotiation.CounterPropose(91m, _now);

        var outcome = negotiation.CounterPropose(92m, _now);

        outcome.ShouldBe(NegotiationOutcome.NoProposalsRemaining);
        negotiation.CurrentOffer.ShouldNotBe(92m);
        negotiation.Status.ShouldBe(NegotiationStatus.Open);
    }

    [Fact]
    public void Accept_closes_negotiation_as_Accepted()
    {
        var negotiation = StartValid();

        negotiation.Accept(_now.AddDays(1));

        negotiation.Status.ShouldBe(NegotiationStatus.Accepted);
        negotiation.DecidedAtUtc.ShouldNotBeNull();
    }

    [Fact]
    public void RejectCurrentOffer_keeps_open_and_stamps_staff_action_without_touching_budget()
    {
        var negotiation = StartValid();

        negotiation.RejectCurrentOffer(_now.AddMinutes(10));

        negotiation.Status.ShouldBe(NegotiationStatus.Open);
        negotiation.LastStaffActionAtUtc.ShouldBe(_now.AddMinutes(10));
        negotiation.ProposalsUsed.ShouldBe(1);
        negotiation.DecidedAtUtc.ShouldBeNull();
    }

    [Fact]
    public void Withdraw_moves_open_negotiation_to_terminal_Withdrawn()
    {
        var negotiation = StartValid();
        negotiation.CounterPropose(90m, _now);

        negotiation.Withdraw(_now.AddHours(1));

        negotiation.Status.ShouldBe(NegotiationStatus.Withdrawn);
        negotiation.DecidedAtUtc.ShouldNotBeNull();
        negotiation.CurrentOffer.ShouldBe(90m); // history preserved
    }

    [Fact]
    public void Terminal_negotiations_refuse_further_operations()
    {
        var withdrawn = StartValid();
        withdrawn.Withdraw(_now);
        var accepted = StartValid();
        accepted.Accept(_now);
        var rejected = StartValid();
        rejected.CounterPropose(500m, _now);

        foreach (var terminal in new[] { withdrawn, accepted, rejected })
        {
            Should.Throw<ClosedNegotiationException>(() => terminal.CounterPropose(50m, _now));
            Should.Throw<ClosedNegotiationException>(() => terminal.Accept(_now));
            Should.Throw<ClosedNegotiationException>(() => terminal.RejectCurrentOffer(_now));
            Should.Throw<ClosedNegotiationException>(() => terminal.Withdraw(_now));
        }
    }

    private sealed class StaticPolicy(int maxProposals, decimal multiplierLimit) : INegotiationPolicy
    {
        public int MaxProposalsPerNegotiation { get; } = maxProposals;

        public decimal ProposalMultiplierLimit { get; } = multiplierLimit;
    }
}
```

Note: `INegotiationPolicy` is `internal` in the module assembly and the test project has `InternalsVisibleTo`, so implementing it here is allowed.

- [ ] **Step 8: Update the integration expectation for auto-rejection**

In `tests/PriceNegotiationApp.IntegrationTests/NegotiationsShould.cs`, inside `Counter_over_limit_auto_rejects_and_closes` (line ~118), change:

```csharp
outcome!.Outcome.ShouldBe("AutoRejected");
outcome.Negotiation.Status.ShouldBe("Rejected");
```

(was `"Declined"`). No other existing integration assertions change: staff decline/accept bodies are only checked via `EnsureSuccessStatusCode`, and owner DELETE still returns 204.

- [ ] **Step 9: Run validation**

```bash
dotnet build && dotnet test tests/PriceNegotiationApp.Modules.Negotiations.Tests
```

Then, with Docker running:

```bash
dotnet test tests/PriceNegotiationApp.IntegrationTests
```

All green. If Docker is unavailable, say so explicitly in the task report.

- [ ] **Step 10: Commit**

```bash
git add src/PriceNegotiationApp.Modules.Negotiations tests/PriceNegotiationApp.Modules.Negotiations.Tests/NegotiationLifecycleShould.cs tests/PriceNegotiationApp.IntegrationTests/NegotiationsShould.cs
git commit -m "feat(negotiations): explicit state machine with snapshotted policy and race-safe creates"
```

---

### Task 3: Identity hardening — sync JWT generation + duplicate-email race

**Files:**
- Modify: `src/PriceNegotiationApp.Modules.Identity/Features/Auth/JwtManager.cs`
- Modify: `src/PriceNegotiationApp.Modules.Identity/Features/Auth/Login.cs:42`
- Modify: `src/PriceNegotiationApp.Modules.Identity/Features/Auth/Register.cs`
- Test: `tests/PriceNegotiationApp.Modules.Identity.Tests/JwtManagerShould.cs`

**Interfaces:**
- Consumes from Task 1: `DbWriteGuard.IsUniqueViolation`.
- Produces:
  - `(string Token, DateTimeOffset ExpiresAtUtc) JwtManager.Generate(Guid userId, string email, IReadOnlyCollection<string> roles)` — synchronous; replaces `GenerateAsync`.

- [ ] **Step 1: Make JwtManager honest about its synchrony**

Replace the method body signature block in `JwtManager.cs` (keep all token-building logic identical):

```csharp
public (string Token, DateTimeOffset ExpiresAtUtc) Generate(Guid userId, string email, IReadOnlyCollection<string> roles)
{
    var settings = options.Value;
    var now = clock.GetUtcNow();
    var expiresAtUtc = now.AddMinutes(settings.ExpiryMinutes);

    var claims = new List<Claim>
    {
        new(JwtRegisteredClaimNames.Sub, userId.ToString()),
        new(JwtRegisteredClaimNames.Email, email),
        new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
    };
    claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

    var credentials = new SigningCredentials(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SecretKey)),
        SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: settings.Issuer,
        audience: settings.Audience,
        claims: claims,
        notBefore: now.UtcDateTime,
        expires: expiresAtUtc.UtcDateTime,
        signingCredentials: credentials);

    return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
}
```

In `Login.cs` line 42, replace:

```csharp
var (token, expiresAtUtc) = jwt.Generate(user.Id, request.Email, roles);
```

- [ ] **Step 2: Map duplicate-email races to 409 in Register**

In `Register.cs`, wrap the create call (replace lines 18–30):

```csharp
var user = new ApplicationUser { UserName = request.Email, Email = request.Email };
IdentityResult result;
try
{
    result = await userManager.CreateAsync(user, request.Password);
}
catch (DbUpdateException ex) when (DbWriteGuard.IsUniqueViolation(ex, out _))
{
    // Two concurrent registrations for the same email: the pre-check inside Identity
    // lost the race, the unique index caught it — surface it as the same conflict.
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
```

Add to the file's usings: `Microsoft.EntityFrameworkCore` (for `DbUpdateException`). `PriceNegotiationApp.SharedKernel` is already imported.

- [ ] **Step 3: Update JwtManager unit tests**

In `tests/PriceNegotiationApp.Modules.Identity.Tests/JwtManagerShould.cs`: drop `async Task` from the test (make it `public void`), remove the `await`, and call:

```csharp
var (token, expiresAtUtc) = sut.Generate(Guid.NewGuid(), "user@test.dev", ["Customer"]);
```

Assertions unchanged.

- [ ] **Step 4: Run validation**

```bash
dotnet build && dotnet test tests/PriceNegotiationApp.Modules.Identity.Tests
```

Green, zero warnings.

- [ ] **Step 5: Commit**

```bash
git add src/PriceNegotiationApp.Modules.Identity tests/PriceNegotiationApp.Modules.Identity.Tests/JwtManagerShould.cs
git commit -m "refactor(identity): sync JWT generation and race-safe duplicate-email conflicts"
```

---

### Task 4: Integration coverage for the new semantics

**Files:**
- Modify: `tests/PriceNegotiationApp.IntegrationTests/NegotiationsShould.cs`

**Interfaces:**
- Consumes from Task 2 (HTTP surface): decline returns `{"outcome":"current_offer_rejected","negotiation":{...}}`; accept returns `{"outcome":"accepted",...}`; owner DELETE → 204 + status `Withdrawn` afterwards; admin DELETE → 204 then 404; concurrent same-customer creates yield exactly one 201, the rest 409.

- [ ] **Step 1: Pin staff-decline outcome in the back-and-forth test**

In `Full_back_and_forth_then_accept`, replace both `StaffDecideAsync(staff, negotiationId, decline: true)` round-1 calls with explicit assertions. New body of round 1:

```csharp
// Round 1: staff rejects the current offer (stays open), customer counters
var decline1 = await staff.Client.PostAsJsonAsync($"/api/v1/negotiations/{negotiationId}/decline", new { }, TestContext.Current.CancellationToken);
decline1.StatusCode.ShouldBe(HttpStatusCode.OK);
var decision1 = await decline1.Content.ReadFromJsonAsync<StaffAction>(Json, TestContext.Current.CancellationToken);
decision1!.Outcome.ShouldBe("current_offer_rejected");
decision1.Negotiation.Status.ShouldBe("Open");
(await CounterProposeAsync(customer, negotiationId, 90m)).StatusCode.ShouldBe(HttpStatusCode.OK);
```

Round 2 keeps using `await StaffDecideAsync(staff, negotiationId, decline: true);`. For the final accept, assert the outcome too:

```csharp
var accept = await staff.Client.PostAsJsonAsync($"/api/v1/negotiations/{negotiationId}/accept", new { }, TestContext.Current.CancellationToken);
accept.StatusCode.ShouldBe(HttpStatusCode.OK);
var accepted = await accept.Content.ReadFromJsonAsync<StaffAction>(Json, TestContext.Current.CancellationToken);
accepted!.Outcome.ShouldBe("accepted");
```

Add the record next to `CounterOutcome`:

```csharp
private sealed record StaffAction(string Outcome, NegotiationView Negotiation);
```

- [ ] **Step 2: Add the withdraw-vs-delete lifecycle test**

Append this test to `NegotiationsShould`:

```csharp
[Fact]
public async Task Owner_withdraw_closes_but_preserves_history_admin_delete_destroys()
{
    var (customer, _, negotiationId) = await StartOpenNegotiationAsync();

    var withdraw = await customer.Client.DeleteAsync($"/api/v1/negotiations/{negotiationId}", TestContext.Current.CancellationToken);
    withdraw.StatusCode.ShouldBe(HttpStatusCode.NoContent);

    var view = await GetNegotiationAsync(customer, negotiationId);
    view.Status.ShouldBe("Withdrawn");
    view.DecidedAtUtc.ShouldNotBeNull();
    view.BasePrice.ShouldBe(100m); // snapshot history intact

    // Withdrawn is terminal
    var counter = await CounterProposeAsync(customer, negotiationId, 50m);
    counter.StatusCode.ShouldBe(HttpStatusCode.Conflict);

    // Only an admin can hard-delete; afterwards it is gone
    var admin = await fixture.LoginAsAdminAsync();
    (await admin.Client.DeleteAsync($"/api/v1/negotiations/{negotiationId}", TestContext.Current.CancellationToken))
        .StatusCode.ShouldBe(HttpStatusCode.NoContent);
    (await admin.Client.GetAsync($"/api/v1/negotiations/{negotiationId}", TestContext.Current.CancellationToken))
        .StatusCode.ShouldBe(HttpStatusCode.NotFound);
}

[Fact]
public async Task Customer_cannot_hard_delete_another_users_negotiation()
{
    var (_, stranger, _) = await StartOpenNegotiationAsync();
    var (_, _, otherId) = await StartOpenNegotiationAsync();

    // stranger is a customer who owns their own negotiation but not otherId;
    // DELETE must be forbidden, not silently withdraw someone else's deal
    (await stranger.Client.DeleteAsync($"/api/v1/negotiations/{otherId}", TestContext.Current.CancellationToken))
        .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
}
```

Also update the stale comment in `Access_matrix_view_and_counter` from "Only owner can withdraw; admin can delete anything" to "Owner withdraw soft-closes; admin hard-deletes" — assertions there stay valid (owner DELETE still 204).

- [ ] **Step 3: Add the uniqueness-race regression test**

The point of F4: whatever interleaving happens, the loser must get 409 — never 500. Exactly one request can win because the partial unique index admits exactly one open row per (product, customer).

```csharp
[Fact]
public async Task Concurrent_creates_produce_single_winner_and_conflicts_never_500()
{
    var product = await CreateProductAsync();
    var customer = await fixture.CreateUserAsync();

    var attempts = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
        customer.Client.PostAsJsonAsync("/api/v1/negotiations",
            new { productId = product.Id, proposedPrice = 80m }, TestContext.Current.CancellationToken)));

    attempts.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
    attempts.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(5);
}
```

Requires `using System.Linq;` is implicit (ImplicitUsings) — no new usings needed beyond what the file already has.

- [ ] **Step 4: Run validation**

With Docker running:

```bash
dotnet test tests/PriceNegotiationApp.IntegrationTests
```

All tests green including the three new ones. If Docker is unavailable, state that explicitly.

- [ ] **Step 5: Commit**

```bash
git add tests/PriceNegotiationApp.IntegrationTests/NegotiationsShould.cs
git commit -m "test(negotiations): cover withdraw lifecycle, staff outcome contract, and create races"
```

---

### Task 5: Documentation sync + full CI-parity validation

**Files:**
- Modify: `README.md`

**Interfaces:** none (docs only).

- [ ] **Step 1: Update README negotiation rules**

In `README.md`, replace rule list items under `## Negotiation rules` with:

```markdown
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
```

Under `## API surface (v1)` no route changes are needed, but add one line after the table:

> Status vocabulary: `Open | Accepted | Rejected | Withdrawn`. `Rejected` is terminal auto-rejection; staff decline responses carry `"outcome":"current_offer_rejected"` while the status stays `Open`.

- [ ] **Step 2: Run full CI-parity validation**

```bash
dotnet format --verify-no-changes --no-restore && dotnet build -c Release && dotnet test
```

This mirrors the GitHub Actions pipeline (format check → Release build → all unit + integration tests). Everything must pass; fix any formatting nits with `dotnet format` before committing.

- [ ] **Step 3: Commit**

```bash
git add README.md docs/superpowers/plans/2026-08-25-negotiation-lifecycle-redesign.md
git commit -m "docs: align negotiation lifecycle rules and status vocabulary with implementation"
```

---

## Self-Review Record

- Spec coverage: F1→Task 2 Steps 1–2, 6–8 (status rename + RejectCurrentOffer + outcome contract); F2→Task 2 Step 6 (Withdraw.cs) + Task 4 Step 2; F3→Task 2 Steps 2–3 (snapshot columns) + migration defaults in Step 4; F4→Task 1 + Task 2 Steps 5–6 + Task 4 Step 3; F5→Task 2 Steps 5–6 (RequireReadOnlyAsync, ListMine short-circuit) + Task 3 Step 1 (JwtManager sync). D4's Identity adoption → Task 3 Step 2. Migration/backfill → Task 2 Step 4. Docs → Task 5.
- Correction vs spec: spec §7 assumed 0-based enum ints ("map legacy Declined(=2)"); actual enums are 1-based (`Declined = 3`), so the rename preserves stored values and **no data remap is required** — only additive columns plus index rename.
- Type consistency check performed: `RemainingProposals()` parameterless everywhere; `ToResponse(Negotiation)` single-param everywhere; `SaveOrConflictAsync(this DbContext, Func<string, Exception>, CancellationToken)` matches call sites; `PostgresException` ctor uses named `constraintName:` argument consistent across Task 1 production/test code.

