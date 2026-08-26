# Transaction Management Pattern Review — Design

- Date: 2026-08-26
- Status: Approved (pending implementation)

- Scope decision: fix the two correctness gaps, enforce handler-owned commits via an
  architecture test (no pipeline behavior), reconcile docs. Verified by new tests +
  full suite.

## Goal

Verify the codebase actually implements the prescribed transaction patterns
prescribes, close the gaps where it does not, and make the compliance story mechanical
rather than disciplinary — without importing machinery the repo's architecture does not
want (MediatR, pipeline behaviors).

## Audit result

**Compliant today**

- Three module-owned scoped `DbContext`s (Identity / Catalog / Negotiations); one
  writing context per use case; cross-module access only through the read-only
  `IProductPriceProvider` port.
- None of the doc's "never"s present: no shared UoW, no repositories over `DbSet`,
  no `TransactionScope`, no per-request transaction filter.
- Client-generated GUIDv7 keys everywhere (`Guid.CreateVersion7()`), which is why every
  use case except creation fits one flush.
- Optimistic concurrency tokens correctly configured (`uint Version` → `xmin` system
  column) on both write aggregates (`Negotiation`, `Product`).
- Creation races on the partial unique index already translate to 409 via
  `DbWriteGuard.SaveOrConflictAsync`.
- Outbox/events rationally deferred until the first subscriber exists (pinned decision,
  ddd-audit spec §F-04); there are no cross-module state workflows to serve.

**Gaps**

| ID | Severity | Gap |
|---|---|---|
| G1 | High | Nobody handles `DbUpdateConcurrencyException`: concurrent accept-vs-counter-propose, dual staff product edits, or delete-vs-update races surface as HTTP 500 instead of the prescribed 409. The tokens fire; nothing reacts. |
| G2 | Medium | `CreateNegotiationHandler` performs two saves and the first commits alone (customer provisioning in `NegotiationAccess.GetOrCreateCustomerIdAsync`). If the negotiation insert later fails, an orphaned customer row persists — the exact mid-flow commit the reference doc forbids. |
| G3 | Low | Doc Option 4 (commit-on-success behavior) absent by design. Accepted — but then nothing mechanically prevents future handlers from scattering saves. |
| G4 | Low | The patterns doc's closing section ("Recommendation for this repo") references `BookingDbContext`/`Worker`/payments — transplanted from another codebase. README's "xmin concurrency" claim is half-true while conflicts map to 500. |

## Fixes

### F-G1 — concurrency conflicts become 409

- Add `ErrorCodes.ConcurrencyConflict = "concurrency_conflict"` to SharedKernel.
- In `GlobalExceptionHandler`, map `DbUpdateConcurrencyException` before the fallback:
  status 409, title "Resource changed meanwhile", code `concurrency_conflict`.
- Handlers stay ceremony-free; the exception propagates from their single
  `SaveChangesAsync`.

Tests:

1. Unit: `GlobalExceptionHandler.TryHandleAsync` given a thrown
   `DbUpdateConcurrencyException` writes status 409 + `code=concurrency_conflict`.
2. Integration (Testcontainers): load the same negotiation through two scopes, mutate
   both, save both — assert the loser throws `DbUpdateConcurrencyException` (proves the
   xmin token fires end-to-end) and, through the mapper test above, maps to 409.

### F-G2 — atomic create-negotiation flow (Option A: explicit transaction)

Wrap provisioning + insert in one explicit transaction inside `CreateNegotiationHandler`
(the reference doc's Case B):

```csharp
await using var tx = await db.Database.BeginTransactionAsync(ct);
var customerId = await NegotiationAccess.GetOrCreateCustomerIdAsync(db, caller.UserId, ct);
var negotiation = Negotiation.Start(...);
await db.Negotiations.AddAsync(negotiation, ct);
await db.SaveOrConflictAsync(
    _ => new ConflictException(NegotiationErrorCodes.NegotiationAlreadyOpen, "..."), ct);
await tx.CommitAsync(ct);
```

Failure anywhere rolls back the provisioned customer together with the failed insert;
the existing unique-violation re-fetch logic inside `GetOrCreateCustomerIdAsync` is
untouched. Rejected alternative: single-flush restructure with constraint-name-aware
recovery — purer commit count but trickier error-path code for no observable gain here.

Test: covered structurally (the transaction block is the guarantee); the full suite
guards the happy path and conflict paths already.

### F-G3 — mechanical enforcement without new dependencies

Add an ArchUnitNET rule to the existing `tests/PriceNegotiationApp.ArchitectureTests`:
invocations of `SaveChangesAsync` are allowed only from

- `*Handler` feature classes (the single commit point per use case),
- module seeding hosted services,
- `DbWriteGuard` (which wraps the call itself).

Any other caller fails the build. This pins the doc's "one commit point, owned by the
handler" invariant the same way transport-only endpoints are already pinned. Deliberate
non-build: MediatR + `TransactionBehavior` — the repo has no mediator pipeline and
recent architecture work deliberately made handlers own persistence.

### F-G4 — docs tell the truth about this repo

- Replace the transplanted closing section of the transaction patterns doc
  ("Recommendation for this repo") with one describing this codebase: compliant points
  above, the deliberate absence of Option 4 enforcement (replaced by the ArchUnit rule),
  outbox deferral rationale, and pointer to this spec.
- README stack-table claim "xmin concurrency" becomes fully true once F-G1 lands; add
  half a sentence noting conflicts return 409.

## Verification

- New tests from F-G1 pass; ArchUnit rule passes.
- Full gate green: `dotnet format --verify-no-changes`, Release build,
  `dotnet test --solution PriceNegotiationApp.slnx -c Release`.

## Out of scope

- MediatR / `TransactionBehavior` (rejected, see F-G3).
- Outbox/integration events (first-subscriber trigger documented in ddd-audit §F-04).
- Isolation-level escalation anywhere (no multi-statement business rule needs it; the
  create-flow now uses a transaction only for atomicity, still READ COMMITTED).
