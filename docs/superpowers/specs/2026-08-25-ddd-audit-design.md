# DDD Audit & Optimized Design — PriceNegotiationApp

Date: 2026-08-25
Question set: strategic vs tactical DDD? proper aggregates? protected invariants? proper
bounded contexts? proper domain/integration event handling?
Verdict up front: **strategic DDD is genuinely implemented and above average for a modular
monolith; tactical DDD is solid at the core aggregate with two real findings and one
deliberate anemia. Events are absent — correctly so today — but the seam is undefined.**

---

## 1. Audit Matrix

| Question | Verdict | Evidence |
|---|---|---|
| Strategic DDD? | **Yes — real** | Three bounded contexts as modules: internal implementations, `InternalsVisibleTo` only to composition root + own tests, one PostgreSQL schema each, zero cross-module type references (ArchUnitNET-enforced) |
| Proper bounded contexts? | **Yes** | Identity = *generic subdomain* (deliberately delegated to ASP.NET Identity, no hand-rolled domain); Catalog = supporting; Negotiations = core. Language inside the core matches the business rules (proposal budget, offer cap, reject-current-offer vs auto-rejection) |
| Context mapping done well? | **Yes** | Single sanctioned edge: consumer-owned port (`IProductPriceProvider` in Negotiations.Ports) satisfied by a composition-root adapter reading Catalog's DbContext — anti-corruption without ceremony |
| SharedKernel right-sized? | **Yes** | Only primitives both sides truly need (CallerContext, paging, error semantics, DbWriteGuard); no leaked domain types |
| Aggregates proper? | **Mostly** | See §2 |
| Invariants protected? | **Core: yes. Cross-aggregate: yes, but placement is implicit** | See F-02 |
| Domain events? | **None exist — correct for current behavior, but the seam is undefined** | F-04 |
| Integration events? | **None exist; no outbox** | Same finding |
| Repositories/UoW? | **DbContext-as-UoW, DbSet-as-collection — deliberate, undocumented** | F-05 |

## 2. Tactical assessment per aggregate

### Negotiation (core, root) — exemplary after the lifecycle redesign
- Explicit state machine (`Open → Accepted/Rejected/Withdrawn`), single decision path,
  terminal states refuse all operations.
- Invariants live **inside**: proposal budget (`ProposalsUsed < MaxProposals`), offer cap
  (`offer ≤ BasePrice × OfferMultiplierLimit`), price positivity via `Price` VO.
- Policy values are **snapshotted at creation** — in-flight negotiations are immune to
  config changes (mirrors BasePrice snapshot philosophy).
- References other aggregates by identity only (`ProductId: Guid`, `CustomerId`) — no
  object navigation across aggregates. Textbook.
- Optimistic concurrency (`xmin`) guards interleaved counter-proposals.

### Product (supporting, root) — sound
- Factory + `Update` enforce name ≤200 trimmed / price > 0; idempotent PUT handled by
  returning change-flag; `xmin` concurrency.

### Customer (core, root?) — deliberately anemic, now formally justified
Two fields, a factory, zero behavior. It exists solely to bind an ASP.NET Identity user to
the negotiations schema. It protects exactly one invariant (non-empty identity link) and is
never mutated after creation. Verdict: keep as-is; it is a **reference row**, not a
behavioral aggregate — but the repo should say so (F-03).

## 3. Findings

### F-01 — Money inconsistency between contexts (real, fix)
Catalog wraps money in `Price` VO; Negotiation stores `BasePrice`, `CurrentOffer`,
`OfferMultiplierLimit` as raw decimals and validates only at the boundary
(`Price.From(offer)`). Two representations of the same ubiquitous concept across the core
domain. Fix: introduce/adopt `Price` VO inside Negotiation for `BasePrice`/`CurrentOffer`
(EF value conversion already proven in Catalog); multiplier stays decimal (it is a ratio,
not money).

### F-02 — Cross-aggregate invariant placement is correct but invisible (document + pin)
*"At most one Open negotiation per (product, customer)"* spans two aggregates, so it cannot
live inside `Negotiation`. Current enforcement is actually the recommended stack — partial
unique index (authoritative), endpoint pre-check (friendly 409 fast-path) — but nothing
records that this is intentional or warns against "moving it into the aggregate".
Fix: doc-comment on the aggregate + configuration, plus an integration assertion already
exists (conflict test). No code change beyond comments.

### F-03 — Anemic Customer (justify in-code)
Add the §2 rationale as XML docs on the entity so nobody "fixes" it into a fake aggregate
later. No behavioral change.

### F-04 — Event strategy undefined (design the seam, implement nothing yet)
Current flows are single-context and synchronous; no subscriber exists. Introducing
domain/integration events now would be speculative machinery. However BF-01 (deal on
acceptance) and BF-04 (notifications) will need them, so define the pattern now:
- **Domain events:** aggregates collect `IDomainEvent` instances; a SaveChanges
  interceptor dispatches to in-process handlers after successful save (same-unit-of-work
  consistency).
- **Integration events:** per-module append-only outbox table written in the same
  transaction; hosted service publishes (in-proc for monolith, broker-ready later).
- **Trigger:** implement the day BF-01 lands — not before. This spec fixes vocabulary and
  location (`Modules.<X>/Domain/Events`, `Persistence/Outbox`) so the first feature doesn't
  invent its own.

### F-05 — Repository/UoW stance undocumented (codify)
EF Core conventions here are deliberate: module DbContext = unit of work, `DbSet<T>` =
aggregate collection, feature classes own queries, no generic repositories, no MediatR.
Record as architecture law in README architecture section; add ArchUnitNET guard rails.

### F-06 — Drift guards missing for DDD rules (cheap, high value)
Existing architecture tests cover modules/kernel/domain-EF-purity. Add three rules:
1. Types in `*.Modules.*.Features*` may depend on EF Core, but types in `*.Domain` must not
   reference `*.Persistence` namespaces (reverse leakage guard).
2. No type named `IRepository`/`Repository` anywhere (prevents ceremony re-entry).
3. Only `Api` composition root references more than one module (already covered) — extend
   with: `SharedKernel` must not reference any module (covered) — skip duplicates; add
   rule: `Ports/*` types never reference `Persistence` (contract purity).

## 4. Approaches considered

- **A. Codify + targeted polish (chosen):** F-01 VO fix, F-02/F-03 documentation, F-05
  README law, F-06 three arch rules, F-04 pattern definition only. ~1 day, zero risk to
  behavior.
- **B. Full tactical treatment now:** repositories, domain-service layer, events infra,
  outbox, CQRS-lite read models. Rejected: speculative complexity with zero subscribers;
  violates YAGNI and this repo's own doctrine.
- **C. Docs-only:** leaves F-01 (genuine model inconsistency) unresolved. Rejected.

## 5. Targeted changes (Approach A)

| # | Change | Files |
|---|---|---|
| 1 | `Price` VO (Vogen) for `Negotiation.BasePrice`, `Negotiation.CurrentOffer`; EF conversions to `numeric(18,2)`; constructor/boundary validation simplifies (VO validates >0); multiplier stays decimal | `Negotiation.cs`, `NegotiationConfiguration.cs`, migration (type-preserving: numeric(18,2)→numeric(18,2), no data change), response mapping untouched externally (JSON still emits number) |
| 2 | Doc-comments: cross-aggregate uniqueness note on `Negotiation` class + index config; anemia rationale on `Customer` | 2 files |
| 3 | Architecture rules (NetArchTest→ArchUnitNET equivalents): Features↔EF allowed, Domain↛Persistence-namespaces, forbidden `IRepository`/`Repository` type names | `ArchitectureShould.cs` |
| 4 | README architecture section: "Tactical DDD laws" block (aggregate collection = DbSet; no repositories; events deferred to BF-01; policy snapshot rule) | README |
| 5 | Event seam vocabulary section appended to spec (§F-04 above) — no code | this doc |

Migration note: column types unchanged (`numeric(18,2)` already), so the EF model diff is
conversion-only → **no database migration required** if conversions map identically;
verify via `dotnet ef migrations has-pending-model-changes` (or add empty migration check)
during implementation.

## 6. Testing strategy

- Existing Negotiation unit suite must stay green unchanged (proves VO swap is
  behavior-preserving); assertions compare `.Value` where they previously compared decimal.
- New unit facts: `BasePrice ≤ 0` rejected through VO path (previously impossible — VO was
  only consulted for offers).
- New ArchUnitNET rules get positive+negative fixtures (negative fixture creates a temp
  offending type in-memory? Not feasible — instead assert rules pass and rely on CI).
- Full MTP suite + format + Release build (CI parity).

## 7. Non-goals

Repositories/UoW abstractions; MediatR; domain events implementation; outbox tables;
changing Customer into a behavioral aggregate; splitting Negotiations further; CQRS read
models.
