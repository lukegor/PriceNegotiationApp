# DDD Tactical Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve audit finding F-01 (money VOs inside the Negotiation aggregate), pin deliberate designs F-02/F-03 with doc-comments, codify repository stance (F-05) and add three architecture drift guards (F-06).

**Architecture:** Swap raw `decimal` money fields in `Negotiation` to the existing Vogen `Price` VO with explicit EF value conversions — type-preserving, no migration. Everything else is comments, one README law block, and three new ArchUnitNET facts. Zero behavior change at any API boundary.

**Tech Stack:** Vogen 8.0.7, EF Core 10 value conversions, ArchUnitNET.xUnitV3 0.13.4, MTP.

## Global Constraints

- Source spec: `docs/superpowers/specs/2026-08-25-ddd-audit-design.md` §5 table.
- **No database migration may be produced**: columns stay `numeric(18,2)`; the change is conversion-only. Prove with `dotnet ef migrations has-pending-model-changes`.
- External JSON contract unchanged (`BasePrice`/`CurrentOffer` remain JSON numbers).
- Semantic literals in tests stay inline per the Bogus doctrine; only `.Value` accessors get added.
- Existing Negotiation unit suite must pass **unchanged in assertions except `.Value` additions** — that proves behavior preservation.
- Every task ends with zero-warning build + touched suites green; shell pwsh from repo root.

---

### Task 1: Price value object inside the Negotiation aggregate

**Files:**
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Domain/Negotiation.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Persistence/Configurations/NegotiationConfiguration.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Features/Negotiations/NegotiationModels.cs` (`ToResponse` only)

**Interfaces:**
- Consumes: existing `Price` VO (`Price.From(decimal)` throws `ValueObjectValidationException` on ≤0; `.Value` exposes decimal).
- Produces: `Negotiation.BasePrice` / `Negotiation.CurrentOffer` become `Price`; public method signatures (`Start`, `CounterPropose`, `Accept`, `RejectCurrentOffer`, `Withdraw`, `RemainingProposals`) keep **decimal parameters** — endpoint contracts do not move.

- [ ] **Step 1: Rewrite Negotiation.cs**

```csharp
namespace PriceNegotiationApp.Modules.Negotiations.Domain;

internal sealed class Negotiation
{
    /// <summary>
    /// Cross-aggregate invariant "at most one Open negotiation per (product, customer)"
    /// cannot live here: it spans aggregates. Enforcement stack is intentional —
    /// partial unique index uq_negotiations_open_product_customer (authoritative),
    /// endpoint pre-check (friendly fast-path 409). Do NOT move it into this class.
    /// </summary>
    /// <summary>Base price snapshot taken at creation; protects ongoing negotiations from later product price changes.</summary>
    public Price BasePrice { get; private set; }

    public Price CurrentOffer { get; private set; }

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
        NegotiationId id, Guid productId, CustomerId customerId, Price basePrice, Price initialOffer,
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
        var basePrice = Price.From(basePriceSnapshot);
        var offer = Price.From(initialOffer);
        var limit = decimal.Round(basePrice.Value * policy.ProposalMultiplierLimit, 2);
        if (offer.Value > limit)
        {
            throw new ProposalExceedsLimitException(limit);
        }

        return new Negotiation(NegotiationId.From(Guid.CreateVersion7()), productId, customerId,
            basePrice, offer, policy, now);
    }

    public NegotiationOutcome CounterPropose(decimal offer, DateTimeOffset now)
    {
        EnsureOpen();
        var candidate = Price.From(offer);
        if (ProposalsUsed >= MaxProposals)
        {
            return NegotiationOutcome.NoProposalsRemaining;
        }

        var limit = decimal.Round(BasePrice.Value * OfferMultiplierLimit, 2);
        if (candidate.Value > limit)
        {
            Status = NegotiationStatus.Rejected;
            DecidedAtUtc = now;
            return NegotiationOutcome.AutoRejected;
        }

        CurrentOffer = candidate;
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
}
```

Note the ordering nuance kept deliberately: budget-exhaustion check runs **before** price
validation, matching previous observable behavior (exhausted budget returns
`NoProposalsRemaining` even for an invalid amount).

- [ ] **Step 2: EF value conversions**

In `NegotiationConfiguration.cs`, replace the two money lines:

```csharp
        builder.Property(n => n.BasePrice).HasConversion(
            price => price.Value, value => Domain.Price.From(value)).HasColumnType("numeric(18,2)");
        builder.Property(n => n.CurrentOffer).HasConversion(
            price => price.Value, value => Domain.Price.From(value)).HasColumnType("numeric(18,2)");
```

(`OfferMultiplierLimit` stays a plain decimal mapped to `numeric(5,2)` — it is a ratio,
not money.)

- [ ] **Step 3: Response mapper**

In `NegotiationModels.cs`, the mapper body becomes:

```csharp
    internal static NegotiationResponse ToResponse(Negotiation n) =>
        new(n.Id.Value, n.ProductId, n.BasePrice.Value, n.CurrentOffer.Value, n.Status.ToString(),
            n.ProposalsUsed, n.RemainingProposals(), n.CreatedAtUtc, n.LastProposalAtUtc, n.DecidedAtUtc);
```

`NegotiationResponse`'s decimal properties are untouched — JSON contract identical.

- [ ] **Step 4: Prove conversion-only (no migration)**

```bash
dotnet build && dotnet ef migrations has-pending-model-changes --context NegotiationsDbContext -p src/PriceNegotiationApp.Modules.Negotiations --framework net10.0
```

Expected output contains "No changes have been made to the model since the last
migration". If it reports pending changes, stop and fix the conversions before continuing.

- [ ] **Step 5: Update unit tests — `.Value` additions + new VO-path facts**

In `tests/PriceNegotiationApp.Modules.Negotiations.Tests/NegotiationLifecycleShould.cs`
apply these mechanical replacements:

```text
negotiation.BasePrice.ShouldBe(100m)        → negotiation.BasePrice.Value.ShouldBe(100m)
negotiation.CurrentOffer.ShouldBe(90m)      → negotiation.CurrentOffer.Value.ShouldBe(90m)
negotiation.CurrentOffer.ShouldNotBe(92m)   → negotiation.CurrentOffer.Value.ShouldNotBe(92m)
negotiation.CurrentOffer.ShouldBe(200m)     → negotiation.CurrentOffer.Value.ShouldBe(200m)
withdrawn.CurrentOffer.ShouldBe(90m)        → withdrawn.CurrentOffer.Value.ShouldBe(90m)
```

Append three new facts covering the now-hardened base-price path:

```csharp
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Start_rejects_non_positive_base_price(decimal badBase) =>
        Should.Throw<ValueObjectValidationException>(
            () => Negotiation.Start(CustomerId.From(_faker.Random.Guid()), _productId, badBase, 80m, _now, Policy));

    [Fact]
    public void CounterPropose_rejects_non_positive_offer()
    {
        var negotiation = StartValid();

        Should.Throw<ValueObjectValidationException>(
            () => negotiation.CounterPropose(0m, _now.AddMinutes(5)));
    }
```

Add `using Microsoft.Extensions.DependencyInjection;`? No — add `using Vogen;` for
`ValueObjectValidationException`.

- [ ] **Step 6: Validate**

```bash
dotnet build && dotnet test tests/PriceNegotiationApp.Modules.Negotiations.Tests --no-build
dotnet test tests/PriceNegotiationApp.IntegrationTests --no-build
```

All green (integration asserts HTTP decimals — unchanged).

- [ ] **Step 7: Commit**

```bash
git add src/PriceNegotiationApp.Modules.Negotiations tests/PriceNegotiationApp.Modules.Negotiations.Tests/NegotiationLifecycleShould.cs
git commit -m "refactor(negotiations): price value object inside aggregate, conversion-only persistence"
```

---

### Task 2: Pin deliberate designs with doc-comments

**Files:**
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Persistence/Configurations/CustomerConfiguration.cs`
- Modify: `src/PriceNegotiationApp.Modules.Negotiations/Domain/Customer.cs`

**Interfaces:** none (comments only). The Negotiation cross-aggregate comment already
landed inside Task 1 Step 1's rewrite.

- [ ] **Step 1: Anemia rationale on CustomerConfiguration**

Above `builder.ToTable("customers");` insert:

```csharp
        // DELIBERATE ANEMIC DESIGN (ddd-audit spec F-03): Customer is a reference row
        // binding an ASP.NET Identity user into this context. It is created once and
        // never mutated; it has no behavioral invariants beyond a non-empty identity
        // link. Do not "enrich" it into a fake aggregate without a real use case.
```

Also mirror one summary line onto the entity:

In `Domain/Customer.cs`, above the class:

```csharp
/// <summary>Reference row binding an Identity user to this context. Intentionally
/// anemic — see CustomerConfiguration for rationale. Do not enrich without cause.</summary>
```

- [ ] **Step 2: Validate + commit**

```bash
dotnet build
git add src/PriceNegotiationApp.Modules.Negotiations
git commit -m "docs(domain): pin deliberate designs — cross-aggregate uniqueness, anemic customer"
```

---

### Task 3: Architecture drift guards (F-06)

**Files:**
- Modify: `tests/PriceNegotiationApp.ArchitectureTests/ArchitectureShould.cs`

**Interfaces:**
- Consumes Task 1 state (Domain namespaces unchanged); existing providers `CatalogTypes`, `IdentityTypes`, `NegotiationsTypes`, `CompositionRoot`, `EntityFramework` already defined in the class.

- [ ] **Step 1: Add three facts**

Append inside `ArchitectureShould`:

```csharp
    [Fact]
    public void Domain_namespaces_never_reach_into_persistence_namespaces()
    {
        var persistence = Types().That().ResideInNamespace($"{Catalog}.Persistence")
            .Or().ResideInNamespace($"{Negotiations}.Persistence")
            .As("persistence namespaces");

        Types().That().Are(catalogDomain).Or().Are(negotiationsDomain)
            .Should().NotDependOnAny(persistence)
            .Check(Architecture);
    }

    [Fact]
    public void Port_contracts_stay_persistence_free()
    {
        var catalogPorts = Types().That().ResideInNamespace($"{Catalog}.Ports").As("catalog ports");
        var negotiationsPorts = Types().That().ResideInNamespace($"{Negotiations}.Ports").As("negotiations ports");
        var persistence = Types().That().ResideInNamespace($"{Catalog}.Persistence")
            .Or().ResideInNamespace($"{Negotiations}.Persistence")
            .As("persistence namespaces");

        Types().That().Are(catalogPorts).Or().Are(negotiationsPorts)
            .Should().NotDependOnAny(persistence)
            .Check(Architecture);
    }

    [Fact]
    public void Repository_ceremony_stays_out_of_the_codebase()
    {
        // F-05 doctrine: module DbContext is the unit of work, DbSet<T> the aggregate
        // collection. A repository layer re-introduces ceremony without payoff here.
        var repositories = Types().That().HaveFullNameContaining("Repository");

        repositories.GetObjects(Architecture).ShouldBeEmpty(
            "repository-style types must not appear; use the module DbContext directly");
    }
```

The two domain providers (`catalogDomain`, `negotiationsDomain`) already exist inside the
`Domain_namespaces_stay_free_of_persistence_concerns` fact — hoist them into class-level
readonly providers so both facts share them:

```csharp
    private static readonly IObjectProvider<IType> CatalogDomain =
        Types().That().ResideInNamespace($"{Catalog}.Domain").As("catalog domain");

    private static readonly IObjectProvider<IType> NegotiationsDomain =
        Types().That().ResideInNamespace($"{Negotiations}.Domain").As("negotiations domain");
```

…and refactor the existing fact to consume those fields (deleting its local copies).

- [ ] **Step 2: Validate**

```bash
dotnet build && dotnet test tests/PriceNegotiationApp.ArchitectureTests --no-build
```

All architecture facts green (8 total).

- [ ] **Step 3: Commit**

```bash
git add tests/PriceNegotiationApp.ArchitectureTests/ArchitectureShould.cs
git commit -m "test(architecture): guard domain/persistence leakage and repository ceremony"
```

---

### Task 4: Tactical DDD laws in README + full CI parity

**Files:**
- Modify: `README.md`

**Interfaces:** none.

- [ ] **Step 1: Append laws to the Architecture section rules list**

After the last `- Modules never reference…` bullet block, add:

```markdown
### Tactical DDD laws

- Module `DbContext`s are the unit of work; `DbSet<T>` is the aggregate's collection.
  No repository/UoW abstractions (enforced by an architecture test).
- Cross-aggregate invariants live at the persistence boundary (partial unique indexes)
  with endpoint fast-paths for friendly errors — never inside a single aggregate.
- Negotiation policy values are snapshotted onto the aggregate at creation; config changes
  never rewrite in-flight negotiations.
- Domain/integration events are intentionally absent until the first real subscriber
  (deal-on-accept / notifications features). Pattern will follow
  `docs/superpowers/specs/2026-08-25-ddd-audit-design.md` §F-04 when triggered.
- Money inside aggregates uses value objects; ratios/multipliers use plain decimals.
```

- [ ] **Step 2: Full CI parity**

```bash
dotnet format --verify-no-changes --no-restore
dotnet build -c Release --no-restore
dotnet test --solution PriceNegotiationApp.slnx -c Release --no-build --report-trx
```

Everything green; fix formatting via `dotnet format` before committing if flagged.

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs: tactical ddd laws added to architecture section"
```

---

## Self-Review Record

- Spec coverage: §5 change 1 → Task 1 (VO swap + conversions + mapper + `.Value` tests +
  two new VO-path facts + pending-changes proof); change 2 → Task 2 (+ aggregate comment
  folded into Task 1 code); change 3 → Task 3 (three guards incl. ports purity);
  change 4 → Task 4 Step 1; change 5 → this plan documents F-04 vocabulary only (spec §3
  F-04 says "implement nothing") ✓.
- Placeholder scan: none.
- Type consistency: `Price.From(decimal)`, `.Value` accessor used uniformly;
  `IObjectProvider<IType>` providers named consistently (`CatalogDomain`,
  `NegotiationsDomain`) after hoist; response mapper keeps decimal JSON contract.

