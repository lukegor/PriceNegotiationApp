# Services, Application & Business Logic — Audit and Optimized Design

Date: 2026-08-25
Scope: `src/PriceNegotiationApp.Modules.*` (domain, features, persistence wiring), cross-module composition, and the tests that pin their behavior.

## 1. Audit Verdict

**The overall architecture is correct and should be kept.** The modular monolith with vertical-slice features, rich aggregates, compile-time module boundaries, consumer-owned ports, and direct `DbContext` injection is what a modern team would build for a system of this size in 2026. Explicitly rejected alternatives (they add ceremony without payoff at this scale):

- Repository/Unit-of-Work layer over EF Core — leaky indirection that duplicates `DbContext`.
- MediatR/CQRS bus — one handler per endpoint with zero pipeline value here.
- Dedicated application-service layer between endpoints and domain — the endpoint lambda *is* the application service; extracting it would only relocate code.
- Domain events/outbox — no cross-module reaction exists today; YAGNI.

The audit did find **five substantive defects** concentrated in the Negotiations module's lifecycle semantics, concurrency mapping, and policy versioning. They are design flaws, not style issues: each produces wrong behavior under realistic conditions.

## 2. Findings

### F1 — "Decline" means two different things (correctness of the model)

- `Negotiation.Decline()` (`Domain/Negotiation.cs:89`) is a **no-op** that only asserts openness; staff decline persists nothing.
- Yet `NegotiationStatus.Declined` is a **terminal state**, set only by auto-rejection inside `CounterPropose` (`Domain/Negotiation.cs:71`).
- Same word, two opposite behaviors: an API action named `decline` that changes nothing, and a status named `Declined` that clients can only reach by over-proposing. The state machine lives partly in method names, partly in a comment (`Features/Negotiations/Accept.cs` neighbor file), partly in README prose.

### F2 — Withdrawal physically deletes business history (data loss by design)

`Withdraw.cs:26` hard-deletes the negotiation row. A customer "withdrawing" an **Accepted** negotiation destroys the record that a deal was struck. The API surface says DELETE but the README calls it "withdraw" — two intents collapsed into destructive storage semantics.

### F3 — Negotiation policy is evaluated live, not snapshotted (retroactive rule change bug)

`INegotiationPolicy` is threaded as a parameter into every aggregate call (`Start`, `CounterPropose`, `RemainingProposals`). Consequences:

- Changing `ProposalMultiplierLimit` or `MaxProposalsPerNegotiation` in config retroactively rewrites the rules for **in-flight** negotiations created under old limits.
- The response mapper (`NegotiationResponses.ToResponse(negotiation, policy)`) drags policy plumbing into presentation; six endpoint signatures carry `INegotiationPolicy` solely to compute `ProposalsRemaining`.

This contradicts the codebase's own snapshot philosophy (BasePrice is snapshotted precisely to be immune to later change).

### F4 — Uniqueness races surface as HTTP 500 instead of 409

The schema already has the right constraints (`CustomerConfiguration.cs:15` unique `identity_user_id`; `NegotiationConfiguration.cs:22-24` partial unique index on open `(product_id, customer_id)`). But the write paths perform check-then-insert (`Create.cs:24-30`, `NegotiationAccess.GetOrCreateCustomerIdAsync`) and never map the resulting `23505` violation to its semantic error. Two concurrent first-negotiation requests → one 201 and one **500**; same for concurrent customer provisioning. The database enforces correctness; the application fails to translate it.

### F5 — Read-path inconsistencies

- `Get.cs` loads a tracked entity for a read-only response (List paths use `AsNoTracking()`).
- `ListMine.cs:22` embeds `customer != null` inside the SQL predicate instead of short-circuiting to an empty page.
- Minor: `JwtManager.GenerateAsync` returns `Task.FromResult` — a sync operation wearing an async contract.

## 3. Optimized Design

Keep modules, slices, ports, DI shape, and persistence layout unchanged. Apply five targeted redesigns.

### D1 — One word, one meaning: explicit negotiation state machine

```
Open ──accept──▶ Accepted        (staff, terminal)
Open ──over-propose──▶ Rejected  (auto-rejection, terminal)
Open ──withdraw──▶ Withdrawn     (owner, terminal)
Open ──reject-current-offer──▶ Open   (staff feedback; budget NOT consumed;
                                       recorded via LastStaffActionAtUtc)
```

- Rename aggregate methods: `Accept()`, `RejectCurrentOffer(now)`, `CounterPropose(...)`, new `Withdraw(now)`. Delete the misleading `Decline()`.
- Status enum becomes `Open | Accepted | Rejected | Withdrawn`. `Rejected` replaces `Declined` as the auto-rejection terminal state.
- Staff decline keeps the negotiation open (unchanged business rule) but is now honest: it stamps `LastStaffActionAtUtc` and the response reports `"outcome": "current_offer_rejected"` so clients observe real state transitions.
- Migration: map legacy `Declined(=2)` rows → `Rejected`; add nullable `last_staff_action_at_utc`.

### D2 — Withdraw = close, Delete = destroy

- Owner `DELETE /negotiations/{id}` → `Withdraw(now)`: sets terminal `Withdrawn`, keeps the row and full history. Withdraw is **not** idempotent: withdrawing an already-terminal negotiation returns 409 `negotiation_closed`, consistent with other transitions.
- Hard delete remains available to Admins only (retention/GDPR-style removal), same route, role-gated.

### D3 — Policy snapshot at creation (fixes F3)

`Negotiation` stores `MaxProposals` and `OfferMultiplierLimit` alongside `BasePrice` at `Start(...)` from the injected singleton policy — the single read of `INegotiationPolicy` in the whole module. Aggregate behavior uses instance values; `RemainingProposals()` becomes parameterless; `INegotiationPolicy` disappears from all feature signatures and from `NegotiationResponses`. In-flight negotiations become immune to config changes, matching the BasePrice precedent. Cost: two small columns; benefit: deterministic historical behavior and a visibly simpler call graph.

### D4 — Translate uniqueness violations at the edge (fixes F4)

SharedKernel gains one helper:

```csharp
public static async Task<T> SaveOrConflictAsync<T>(
    this DbContext db, Func<string, Exception> conflict, CancellationToken ct)
```

It calls `SaveChangesAsync`, catches `DbUpdateException` where the inner `PostgresException.SqlState == "23505"`, extracts the constraint name, and throws `conflict(constraintName)`. Usage:

- `Create.cs`: constraint `uq_open_negotiation_product_customer` → `ConflictException("negotiation_already_open", ...)`.
- `GetOrCreateCustomerIdAsync`: unique `ix_customers_identity_user_id` → refetch existing customer and continue (idempotent upsert semantics).

The check-then-insert pre-checks stay (fast path, friendly 409 before work), but they are no longer load-bearing for correctness. Same helper adopted in Identity register path for duplicate-email races.

### D5 — Read-path hygiene (fixes F5)

- `Get.cs` uses `AsNoTracking()`.
- `ListMine` returns an empty page when the caller has no Customer row.
- `JwtManager.Generate` renamed sync (drop `Task` wrapper).

## 4. Data Flow (post-change, counter-propose example)

1. `PATCH /negotiations/{id}/proposals` → endpoint resolves caller, loads owned negotiation.
2. `negotiation.CounterPropose(price, now)` consults **snapshotted** limits; may return `NoProposalsRemaining`, transition to `Rejected`, or apply the offer.
3. Outcome enum mapped once at the endpoint: conflict → 409 ProblemDetails, success → 200 with outcome + response.
4. `SaveOrConflictAsync` guards the flush; unique-violation races cannot escape as 500s.

No new layers, no new abstractions beyond one helper and one enum rename.

## 5. Error Handling

Unchanged RFC 7807 + stable `code` contract, plus: `23505` violations are always translated (constraint→code table lives next to the DbContext configurations); `ClosedNegotiationException` continues to map to `negotiation_closed`.

## 6. Testing Strategy

- **Unit (Negotiations.Tests)**: state-machine matrix — accept/reject-current-offer/withdraw/counter paths from every state; policy snapshot immutability (create under limit A, counter under limit B must use A); withdraw preserves row data.
- **Unit (Catalog.Tests)**: untouched.
- **Integration**: concurrent double-create → exactly one 201, one 409 `negotiation_already_open`; owner withdraw then admin hard-delete; staff decline leaves status Open with updated `last_staff_action_at_utc`; legacy-row migration test seeding `status=2`.

## 7. Migration & Rollout

Single EF migration per affected context (Negotiations only): enum remap `Declined→Rejected`, added columns (`max_proposals`, `offer_multiplier_limit`, `last_staff_action_at_utc`), backfill from current constants. API contract change limited to status string values (`Declined`→`Rejected`, possible new `Withdrawn`) — documented as v1 additive/breaking-minor since consumers are internal.

## 8. Non-Goals

Repository/UoW layers, MediatR, separate application-service classes, domain events/outbox, multi-user staff assignment, audit-log tables beyond retained negotiation history.
