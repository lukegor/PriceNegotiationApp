# Unit of Work & Concurrency Strategy in ASP.NET (EF Core)

> **Question:** what's the optimal approach — ad-hoc `SaveChanges()` here and there, a global unit of work across clean-architecture modules, or something else?
>
> **Short answer:** something else. `DbContext` *already is* a unit of work — and in a modular/DDD codebase you typically have **many** of them, one per bounded context. The modern consensus: **each module owns its own scoped DbContext; every use case has exactly one commit point, in exactly one of those contexts**, optionally enforced by a pipeline behavior. Real write conflicts → **optimistic concurrency**. Cross-module workflows → **events + outbox**, never a shared transaction or a shared UoW. In microservices the same answer splits cleanly along the seam: **Options 3+4 *inside* every service, Option 5 (outbox/sagas) *between* services.**

---

## 0. The problem, in one picture

Concurrency means many HTTP requests hit the same tables simultaneously. Two things must be true:

1. **Each business operation is atomic** — either all of it persists or none of it does.
2. **Simultaneous conflicting writes are detected**, not silently overwritten (lost update problem).

Ad-hoc saves break #1; no strategy alone solves #2 without concurrency tokens.

```csharp
// What goes wrong with ad-hoc SaveChanges:
public async Task<Guid> BookTicket(BookTicketCommand cmd)
{
    var booking = Booking.Create(...);
    _db.Bookings.Add(booking);
    await _db.SaveChangesAsync();      // commit #1 — already visible to everyone

    var payment = await _payments.ChargeAsync(cmd.CardToken, cmd.Amount);
    booking.MarkPaid(payment.TransactionId);
    await _db.SaveChangesAsync();      // commit #2
}
```

If the process dies (or `_payments` throws) between commit #1 and commit #2, the database contains a **booking that was never paid for and can never be rolled back** — commit #1 is permanent. No unit-of-work pattern added *later* fixes code written this way; the save points themselves are the bug.

---

## 1. The options

Seven candidate strategies found in real ASP.NET + EF Core codebases, ordered roughly from what-not-to-do to what-to-do — including the two "legacy defaults" many teams still live with (per-request filters and ambient `TransactionScope`).

### Option 1 — Ad-hoc `SaveChanges()` wherever convenient

**What it is:** every service method (or worse, every few lines) calls `SaveChangesAsync()` whenever it feels data "should" go to the DB now. Each call opens its own autocommit transaction and **commits immediately** — so a use case with five saves is really five independent mini-transactions, none aware of the others. Transaction boundary = whatever line the developer happened to save on.

| Criterion | Score |
|---|---|
| Simplicity (initial) | 65 |
| Correctness / atomicity | **25** |
| Testability | 30 |
| Performance | 45 |
| Long-term maintainability | 20 |
| Clean-architecture fit | 25 |
| Concurrency safety | 20 |
| Developer experience | 40 |
| **Average** | **34** |

**Popularity: ~30%** — dominant in tutorials, junior codebases, and apps that never met a failure mid-operation.

**Why it fails:** with N saves there are N commit points, and no mechanism rolls back the earlier ones when step 3 fails:

- **partial commits on failure** — saves #1..k are permanent, the rest never run; compensation code is left to write by hand;
- **half-written state is visible** to other concurrent requests between save #1 and save #N;
- **unclear ownership** — nobody can say which layer owns "the" transaction, because there isn't one;
- **no retry story** — outbox/idempotent-replay patterns need a single atomic unit to retry; here there is none.

The crucial nuance: multiple saves per use case are **not inherently wrong** — they're wrong when each one *commits alone*. Compare Option 3's fallback ("several saves inside one transaction").

**Verdict:** acceptable only in throwaway prototypes.

---

### Option 2 — Global Unit of Work abstraction shared across modules

**What it is:** the classic DDD-era pattern: repositories register changes without persisting them; a separate `IUnitOfWork` wraps the context and exposes `CommitAsync()`; somebody calls it once per request. In practice the UoW is injected broadly — including across module boundaries — precisely so everyone "shares" one commit.

```csharp
public interface IUnitOfWork : IDisposable
{
    IRepository<Booking> Bookings { get; }
    IRepository<Event>   Events { get; }
    Task<int> CommitAsync(CancellationToken ct);
}

// usage — but in a ServiceA → ServiceB → Repository call chain, WHO owns this call?
await _uow.Bookings.AddAsync(booking);
await _uow.CommitAsync(ct);
```

| Criterion | Score |
|---|---|
| Simplicity | 35 |
| Correctness / atomicity | 55 |
| Testability | 45 |
| Performance | 50 |
| Long-term maintainability | 35 |
| Clean-architecture fit | 40 |
| Concurrency safety | 45 |
| Developer experience | 45 |
| **Average** | **44** |

**Popularity: ~18%** — legacy enterprise codebases, declining steadily since ~2019 as "DbContext is enough" became mainstream advice.

**Why it's an anti-pattern today:**

- `DbContext` **already implements Unit of Work** (its change tracker batches all changes into one transaction on `SaveChanges`) and `DbSet<T>` **already is the repository**. Wrapping them adds ceremony without adding capability.
- Generic repositories leak anyway (`Include`, projections, `IQueryable`) — you end up with `repository.Query().Include(...).Where(...)` = repository with extra steps.
- Mocking `IUnitOfWork` in tests is painful and tests nothing real; testing against a real context is easier.
- **Sharing one UoW across clean-architecture modules is actively harmful:** modules become coupled through one shared context/schema/lifetime, one module's uncommitted changes leak into another's reads (change tracker pollution), and cross-module transactions are a distributed-systems smell pretending to be a local one.
- **It doesn't even solve its own core problem:** deciding *who* calls `CommitAsync` in a deep call graph (`Controller` → `ServiceA` → `ServiceB`, both using repositories) yields either double-commit bugs or hidden filter magic — i.e., the same discipline problem Option 4 solves properly, minus the enforcement.
- **And it doesn't help with the ID problem either:** whether you call `_db.SaveChangesAsync()` or `_uow.CommitAsync()`, needing an entity's database-generated ID mid-flow is identical work. The abstraction adds nothing where it's supposedly needed.

**Verdict:** reject. This is the trap the question hints at.

---

### Option 3 — Scoped DbContexts (one per module), one commit point per use case ✅ baseline

**What it is:** register **each module's** DbContext as **scoped** (one instance per module per HTTP request). The handler mutates tracked entities freely during execution and flushes at the end — **one commit point per use case**, which EF Core turns into an implicit transaction. Note the precise invariant: it's *one commit*, not literally *one `SaveChanges` call*. When a later step in the flow genuinely needs earlier data persisted (IDs, raw SQL, stored procedures), you add more saves — but inside one explicit transaction, so the commit point stays single. See below.

One more silent premise worth making explicit: **everything here also runs at the database's default transaction isolation level** (READ COMMITTED on SQL Server and PostgreSQL). The model above buys you atomicity and hides uncommitted writes from others — but it does *not* isolate you from every interleaving of concurrent transactions. When that matters, isolation has to be chosen deliberately; see "What if another isolation level is needed?" further down.

```csharp
// Composition root — one scoped context PER MODULE, disposed with the request
builder.Services.AddDbContext<BookingDbContext>(o => o.UseNpgsql(cs));    // Bookings module
builder.Services.AddDbContext<PaymentsDbContext>(o => o.UseNpgsql(cs));   // Payments module

// A handler works ONLY with its own module's context:
public sealed class BookTicketHandler(BookingDbContext db)                // Bookings module
{
    public async Task<Guid> Handle(BookTicketCommand cmd, CancellationToken ct)
    {
        var ev = await db.Events.SingleAsync(e => e.Id == cmd.EventId, ct);
        var booking = Booking.Create(ev, cmd.Seats);
        db.Bookings.Add(booking);
        ev.ReserveSeats(cmd.Seats);

        await db.SaveChangesAsync(ct);   // ← single commit point: all-or-nothing
        return booking.Id;
    }
}
```

The rule generalizes cleanly to N modules: **N scoped contexts, each an independent unit of work; one use case → one context → one commit point.** A use case that seems to need two contexts is really two use cases (see the playbook after Option 5).

#### "But my flow can't survive on one SaveChanges!" — yes it can, here's how

Real flows often seem to require persistence *before* the end of the use case: a later step needs an entity's generated ID, or must reference a row created earlier in the same command. Three facts dissolve most of these cases:

1. **EF Core already orders everything within one `SaveChanges`.** Inserts are sorted topologically by FK dependencies (parents before children), and database-generated IDs are propagated across the whole object graph inside that same flush. Booking → Tickets works with identity columns and zero extra effort.
2. **Client-generated IDs remove nearly all remaining cases.** With `Guid.CreateVersion7()` (.NET 9+), ULIDs or HiLo/sequence keys, an entity has its real ID from the moment it's constructed — usable for events, logs, external references before any save happens.
3. **If a step truly requires rows already in the DB** (legacy identity keys consumed by raw SQL or stored procedures, read-your-own-writes via SQL), use several saves **inside one explicit transaction**. Atomicity comes from the transaction, not from counting saves.

```csharp
// Case A — client-generated ID: one flush covers everything
var booking = new Booking(Guid.CreateVersion7(), ev, cmd.Seats);   // real ID from birth
db.Bookings.Add(booking);
foreach (var seatId in cmd.Seats)
    db.Tickets.Add(new Ticket(booking.Id, seatId));                // references ID freely
await db.SaveChangesAsync(ct);                                     // EF orders inserts itself
```

```csharp
// Case B — legacy identity key + a step that genuinely needs persisted rows:
await using var tx = await db.Database.BeginTransactionAsync(ct);

db.Bookings.Add(booking);
await db.SaveChangesAsync(ct);            // save #1 — visible ONLY inside this tx

var reservation = await _seatMap.ReserveAsync(booking.Id);         // needed booking.Id
booking.AttachSeatReservation(reservation.Handle);

await db.SaveChangesAsync(ct);            // save #2
await tx.CommitAsync(ct);                 // BOTH saves become permanent together —
                                          // failure anywhere rolls back BOTH
```

> **The invariant, stated honestly:** any number of `SaveChanges` calls are safe as long as they share one transaction and none commits alone. What's forbidden is letting an intermediate save become permanent while later steps can still fail — that's Option 1 again.

Two closing notes: if you own the schema, prefer fixing Case B at the root by switching keys to client-generated GUIDs rather than carrying two-save patterns forever. And if the "later step" is an *external* call that merely wants an ID (charge card with booking reference), that call doesn't belong mid-command at all — move it behind after-commit events/outbox (Option 5).

#### What if another isolation level is needed?

The default — **READ COMMITTED** (SQL Server: plain locking READ COMMITTED unless the DB enables RCSI; PostgreSQL: MVCC variant of the same) — gives you per-statement consistency: you never see anyone's uncommitted data, and each statement sees a committed snapshot at its moment of execution. What it does **not** give you is stability across statements: another transaction can commit between your `SELECT` and your `UPDATE`, producing lost updates or write skew. The baseline recipe accepts that and closes the gap with short transactions + optimistic concurrency tokens (section 3). That's correct for ~95% of commands.

Reach for a stronger level only when a business rule itself spans multiple statements and interleaving breaks it — the classic example being *"count free seats, then insert booking"*:

| Level | Use when | Cost / caveat |
|---|---|---|
| **READ COMMITTED** *(default)* | normal CRUD; conflicts delegated to concurrency tokens | none |
| **SNAPSHOT** (SQL Server) | long multi-read flows needing one consistent view without blocking writers | version-store/tempdb overhead |
| **REPEATABLE READ** | rows re-read inside one tx must not change underneath you | PG: snapshot-based; SQL Server: shared locks held to commit |
| **SERIALIZABLE** | rule forbids phantoms/write-skew ("check capacity, then insert" must be atomic vs concurrent bookings) | highest contention; abort-and-retry is *expected* operation |

Escalating in EF Core — either imperatively:

```csharp
await using var tx = await db.Database.BeginTransactionAsync(
    System.Data.IsolationLevel.Serializable, ct);

// capacity check + insert now execute against a serialized world;
// concurrent conflicting commits are rejected instead of silently interleaving

await tx.CommitAsync(ct);
```

or declaratively, letting the Option 4 behavior pick the level per command:

```csharp
public interface IIsolationScopedCommand { IsolationLevel Level { get; } }

// in TransactionBehavior:
var level = request is IIsolationScopedCommand c ? c.Level : IsolationLevel.ReadCommitted;
await using var tx = await db.Database.BeginTransactionAsync(level, ct);
```

Rules of thumb when escalating:

1. **Escalate one command, never a module or request.** Contention scales brutally with time-under-lock; a SERIALIZABLE wrapper around everything turns the load test into a deadlock generator.
2. **Every escalated command needs a retry loop.** SQL Server throws deadlock (error 1205)/update conflict; PostgreSQL's SERIALIZABLE aborts with serialization failure (40001). These are normal traffic under contention — translate to HTTP 409/503 + retry, don't log them as bugs.
3. **Before escalating, check the cheaper alternatives**: an optimistic token on the contested aggregate, or one atomic conditional statement (`UPDATE Events SET SeatsAvailable -= @n WHERE Id = @id AND SeatsAvailable >= @n`) achieves most SERIALIZABLE guarantees at READ COMMITTED prices. Escalation is the last tool, not the first.

| Criterion | Score |
|---|---|
| Simplicity | 90 |
| Correctness / atomicity | 80 |
| Testability | 88 |
| Performance | 85 |
| Long-term maintainability | 85 |
| Clean-architecture fit | 85 |
| Concurrency safety | 70 |
| Developer experience | 88 |
| **Average** | **84** |

**Popularity: ~28%** — the default style of serious modern EF Core codebases.

**Strengths:** zero ceremony; change tracker accumulates work cheaply in memory, one short transaction at the end holds locks briefly (great under concurrency); trivially testable (real context + testcontainers/SQLite); scales linearly to modular/DDD designs — N modules simply means N independent contexts.

**Weakness:** correctness relies on **discipline** — nothing mechanically stops a developer from calling `SaveChanges` where no enclosing transaction exists (the exact bug from Option 1). That gap is what Option 4 closes.

---

### Option 4 — Commit-on-success pipeline behavior, resolved per module ✅ enforcement

**What it is:** same as Option 3, but the commit is moved out of handlers into infrastructure: a MediatR pipeline behavior (or ASP.NET action filter, or Scrutor decorator) opens a transaction before the handler runs and commits only if the handler returns successfully. The behavior's final `SaveChangesAsync` is the normal single flush — but a handler that legitimately needs intermediate persistence may call it mid-flow too, staying inside the behavior's transaction either way. With multiple module contexts, the behavior resolves which context serves each request (here: marker interfaces on commands). Handlers contain **zero** transaction ceremony; atomicity is guaranteed by convention instead of discipline.

```csharp
// Commands announce their module; the behavior picks the matching context:
public interface IBookingsCommand : ICommand { }
public interface IPaymentsCommand : ICommand { }

public sealed class TransactionBehavior<TRequest, TResponse>(
    BookingDbContext bookingsDb,
    PaymentsDbContext paymentsDb)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var db =
            request is IBookingsCommand ? bookingsDb :
            request is IPaymentsCommand ? paymentsDb :
            null;                                          // queries / non-persisted requests

        if (db is null)
            return await next();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var response = await next();
            await db.SaveChangesAsync(ct);                 // the ONLY save for this use case — unless the handler flushed mid-flow; then this is a no-op
            await tx.CommitAsync(ct);
            return response;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
```

Injecting all contexts into one behavior is fine — it lives in the composition root/Infrastructure, not in domain code. (Contexts are cheap to construct and EF opens a DB connection only on first real use, so a request touching one module doesn't pay for the others.) Alternatives: one behavior per module registered for that module's requests, or a Scrutor decorator applied per module.

// Handler shrinks to pure domain orchestration (Bookings module):
public sealed class BookTicketHandler(BookingDbContext db)
{
    public async Task<Guid> Handle(BookTicketCommand cmd, CancellationToken ct)
    {
        var ev = await db.Events.SingleAsync(e => e.Id == cmd.EventId, ct);
        var booking = Booking.Create(ev, cmd.Seats);
        db.Bookings.Add(booking);
        ev.ReserveSeats(cmd.Seats);
        return booking.Id;                                 // committed by the behavior
    }
}

| Criterion | Score |
|---|---|
| Simplicity | 75 |
| Correctness / atomicity | **92** |
| Testability | 82 |
| Performance | 83 |
| Long-term maintainability | 88 |
| Clean-architecture fit | **90** |
| Concurrency safety | 72 |
| Developer experience | 87 |
| **Average** | **84** |

**Popularity: ~14%** — growing fast; standard in MediatR-based codebases and modular monoliths.

**Caveats worth knowing:**
- External side effects inside the handler (HTTP payment charge, email send) happen **inside the open transaction**. Keep external calls out of the command handler, or move them behind events published after commit (which pairs naturally with Option 5's outbox).
- One behavior = **one transaction** (one commit) per command — not necessarily one `SaveChanges` call. A handler may flush mid-flow when a step genuinely needs earlier rows; everything still commits or rolls back together. If a command needs multiple sequential *commits*, that's a design smell to fix, not a pattern to extend.
- Mid-flow saves are also the natural place to notice an external side effect trying to sneak inside your transaction — treat that as a signal to move it behind events/outbox instead.
- **Never let one handler commit two module contexts** — that would be a distributed transaction in disguise. Cross-module workflows go through events/outbox (playbook below).

**Verdict:** best-in-class when paired with Option 3 — 3 defines the shape, 4 enforces it.

---

### Option 5 — Module-owned DbContexts + domain events / outbox (cross-module consistency)

**What it is:** each clean-architecture module owns its own DbContext and commits independently — internally following the same one-commit-point rule (multiple flushes allowed, but only within its own local transaction). There is deliberately **no shared unit of work across module boundaries**. Instead, a module writes its state change *plus* an event record to an outbox table **in the same local transaction**, and a background dispatcher publishes those events; other modules react asynchronously with their own transactions.

```csharp
// Inside the Sales module — ONE local transaction:
db.Bookings.Add(booking);
ev.ReserveSeats(cmd.Seats);
db.OutboxMessages.Add(new OutboxMessage(
    type: "booking.created",
    payload: JsonSerializer.Serialize(new BookingCreated(booking.Id, ev.Id))));
await db.SaveChangesAsync(ct);          // state + event are atomic together

// Worker later publishes outbox rows; PaymentsModule consumes with ITS OWN DbContext:
// consume → create Payment row → publish "payment.confirmed" → Sales marks paid.
```

| Criterion | Score |
|---|---|
| Simplicity | 45 |
| Correctness / atomicity (local) | 85 |
| Testability | 78 |
| Performance | **92** |
| Long-term maintainability | 72 |
| Clean-architecture fit | **95** |
| Concurrency safety | 82 |
| Developer experience | 55 |
| **Average** | **76** |

**Popularity: ~10%** — niche but rising; standard in serious modular monoliths and the mental model of microservices done right.

**Trade-off:** consistency across modules becomes **eventual** ("booking exists, payment confirmation arrives a second later") instead of immediate. That's a product decision, not just a technical one. Within a single module you still use Options 3/4 — this option governs only the *boundaries*.

**Verdict:** mandatory *at module seams* once you have more than one module — which is the normal state of a DDD/modular codebase, not an exotic one. Unnecessary inside a single module.

---

---

### Option 6 — One transaction per HTTP request (action filter / middleware)

**What it is:** infrastructure opens a transaction at the start of every mutating HTTP request and commits when the response turns out successful (2xx); rolls back otherwise. Usually an `IAsyncActionFilter` attribute on controllers or middleware around minimal-API endpoints. Services just use the scoped context — nobody calls `BeginTransaction` explicitly, and the HTTP layer owns atomicity.

```csharp
public sealed class TransactionalAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext ctx, ActionExecutionDelegate next)
    {
        var db = ctx.HttpContext.RequestServices
                     .GetRequiredService<BookingDbContext>();

        await using var tx = await db.Database.BeginTransactionAsync();
        var executed = await next();

        if (executed.Exception is null &&
            executed.Result is not ObjectResult { StatusCode: >= 400 })
        {
            await db.SaveChangesAsync();     // single flush for the whole action
            await tx.CommitAsync();
        }
        // disposing without commit rolls everything back
    }
}

[HttpPost, Transactional]
public IActionResult Book(BookTicketCommand cmd) { ... }   // zero save ceremony here either
```

| Criterion | Score |
|---|---|
| Simplicity | 80 |
| Correctness / atomicity | 70 |
| Testability | 75 |
| Performance | 60 |
| Long-term maintainability | 65 |
| Clean-architecture fit | 60 |
| Concurrency safety | 65 |
| Developer experience | 78 |
| **Average** | **69** |

**Popularity: ~12%** — very common in plain MVC / non-CQRS apps (action-filter flavor); in minimal-API apps the same shape appears as `AddEndpointFilter` transaction filters. The natural first "serious" step up from ad-hoc saves.

**Why it's weaker than Option 4:**

- **Commit is decided by HTTP status codes.** Business correctness becomes load-bearing on response mapping: exception-middleware ordering, result filters, and "400 vs exception" conventions silently change whether data commits.
- **Transaction scope = request scope.** A bulk endpoint performing several independent logical operations shares one transaction; one failure nukes all of them even when most succeeded legitimately.
- **Only exists for HTTP.** Background jobs, message consumers, hosted services don't pass through the filter — you'll reinvent Option 4 there anyway, so you end up maintaining two commit mechanisms.
- Encourages "one request = one use case" thinking that breaks as endpoints grow.

**Verdict:** acceptable default for small MVC CRUD apps and admin panels; becomes structurally wrong once requests do more than one logical thing.

---

### Option 7 — Ambient transactions (`TransactionScope`)

**What it is:** .NET's `System.Transactions` ambient model from the .NET Framework era: open a scope block, and any connection created inside enlists automatically; call `Complete()` to commit on dispose. Historically THE way to span multiple resources atomically — which is exactly why it's dangerous with EF Core today.

```csharp
using var scope = new TransactionScope(
    TransactionScopeOption.Required,
    new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
    TransactionScopeAsyncFlowOption.Enabled);      // MANDATORY for await — omit it and
                                                   // the tx silently doesn't flow

await _bookingDb.SaveChangesAsync(ct);
await _auditDb.SaveChangesAsync(ct);               // second context enlisted →
                                                   // escalation to MSDTC risk
scope.Complete();
```

| Criterion | Score |
|---|---|
| Simplicity | 55 |
| Correctness / atomicity | 50 |
| Testability | 45 |
| Performance | **35** |
| Long-term maintainability | 40 |
| Clean-architecture fit | 30 |
| Concurrency safety | 50 |
| Developer experience | 45 |
| **Average** | **44** |

**Popularity: ~7%** — legacy enterprise carryover, steadily fading.

**Why it lost:**

- **Around one context it adds nothing** over `BeginTransactionAsync` — same transaction, more ceremony, ambient magic.
- **Around two contexts it escalates to a distributed transaction**: SQL Server pulls in MSDTC (a Windows service — hostile to containers/cloud); Npgsql/PostgreSQL refuses distributed enlistment outright and throws. The pattern's only superpower is its biggest liability (see playbook rule 3).
- **Async pitfalls**: forget `TransactionScopeAsyncFlowOption.Enabled` and continuations run *outside* your transaction — code that passes tests and corrupts data under load.
- **Silent SERIALIZABLE default.** Without an explicit `TransactionOptions`, every scope runs at `IsolationLevel.Serializable` — far stricter than anyone assumes — multiplying lock contention and deadlock risk while looking like ordinary transactions in code review. (Confirmed in the wild: see Case 5.)
- Ambient state is invisible in method signatures — testability and reasoning suffer.

#### "But I've seen this run in production!"

A very common report — and worth dissecting, because it reveals the actual failure model. Multi-context `TransactionScope` deployments historically survived through a few recurring shapes. Provenance is explicit throughout: **Cases 1–3 are generic industry patterns**, while **Cases 4–5** document two different real repositories — **R1** (keyed-UoW setup; observed first-hand, reconstructed from memory, *not* re-audited) and **R2** (ambient endpoint filter; verified by an automated read-only scan):

**Case 1 — effectively single-resource scope.** Only one context did writes inside the block (or was touched at all). Single-phase promotion (PSPE) keeps *one* enlisted connection fully local — no MSDTC involved. Those scopes were genuinely fine, and they're the majority.

**Case 2 — escalation absorbed by MSDTC.** On-premises Windows fleets run the MS DTC service by default. When a second context enlisted, the transaction went distributed *transparently* — and kept working until someone hit the classic bug class: DTC security/firewall/RPC configuration differing between environments, a move toward Linux containers, or cloud PaaS with no DTC. This matches "distributed transactions only ever appeared as bugs": the mechanism functioned until the environment shifted underneath it. Correctness that depends on infrastructure configuration is correctness on borrowed time.

**Case 3 — nothing atomic was actually happening.** Whether the second context truly joins the ambient transaction depends on subtle details — `Enlist=` connection-string settings, pool-reuse and open/close ordering. Some long-running "working" setups were quietly committing parts independently and surviving because mid-flow failures were rare enough to patch by manual reconciliation.

**Case 4 — real repository R1, observed first-hand *(reconstructed from memory, not re-audited)*: keyed UoWs dodge the problem entirely.** R1's signature move — keyed `IUnitOfWork`s with manual saves in the middle — deserves separate decoding, because that pattern is the interesting one:

```csharp
using var bookingsUow = _uowFactory.Create("bookings");   // wraps BookingDbContext
using var auditUow    = _uowFactory.Create("audit");      // wraps AuditDbContext

// ... mutate bookings ...
await bookingsUow.SaveChangesAsync(ct);   // stage 1 — commits its own local tx

// ... mutate audit ...
await auditUow.SaveChangesAsync(ct);      // stage 2 — best-effort follow-up
```

What's really going on here: each keyed UoW keeps its own context's transaction **single-context** so escalation never triggers; cross-context consistency is maintained by *sequenced stages* — stage 1 permanent, stage 2 best-effort with manual retry/reconciliation when it fails. In other words, **a hand-rolled saga**. It worked — but every guarantee lived in tribal knowledge: nothing stops a future developer from putting the wrong two saves adjacent, and the "what do we do when stage 2 failed?" procedure lives in ops runbooks rather than code. (The keying itself usually served a simpler purpose too: routing work to the right module's context/connection — which is Option 5's ownership rule emerging informally.)

That's exactly the gap modern patterns close: keyed contexts became **module-owned DbContexts** (Option 5), and staged-save-plus-hope became an explicit **outbox + consumer**, where stage 2 is retried mechanically and idempotently instead of remembered culturally. Seen this way, R1 wasn't proof that Option 7 scales — it was Option 5 assembled by hand under deadline pressure, wearing Option 7's jacket.

**Case 5 — real repository R2, agent-audited: ambient endpoint filter over module-owned contexts.** Verified structure of R2 (a second production system, unrelated to R1): one shared `TransactionFilter` registered via `AddEndpointFilter`, wrapping 36 write endpoints across four modules; inside it a single `new TransactionScope(TransactionScopeAsyncFlowOption.Enabled)`; `scope.Complete()` fires only when the response status is < 400; repositories call `SaveChangesAsync` themselves; **no unit-of-work abstraction exists at all** — unlike R1. Seven module-owned scoped contexts (two business, order, notification, authorization, file, outbox) all register against **one shared SQL Server connection string**; the primary runtime is **Linux Docker (.NET 10)**.

Three distinct patterns live inside that filter:

- **A · single writer (the majority):** one business context writes; other contexts untouched. One enlisted resource → stays fully local via PSPE. Genuinely fine.
- **B · cross-module read + single write:** query module X's context, write module Y's. Still only one *writing* connection; strictly sequential awaits. Works.
- **C · dual writers in one scope:** a release flow writes stock via one context, then order state via another — sequential `SaveChangesAsync` calls from two contexts under one ambient scope.

Pattern C is textbook escalation territory: two enlisted connections inside one scope is exactly what triggers promotion — and on a Linux container runtime there is no MSDTC standing by to absorb it. Whether any given execution actually throws depends on details invisible in source (physical connection lifetimes, pool behavior, precise open/close ordering — the audit lists these as explicit unknowns). That uncertainty *is* the diagnosis: intermittent, environment-dependent failures — the same "distributed transactions only ever showed up as bugs" signature as Cases 2–3, except modern .NET on Linux doesn't even ship the safety net.

Two portable lessons fall out of the audit:

1. **`TransactionScope`'s default isolation is SERIALIZABLE, not READ COMMITTED.** Passing no `TransactionOptions` silently ran all 36 endpoints at Serializable — maximum locking, deadlock-prone — while reading as "plain transactions" in review; and the repo contains no 1205/40001 retry helper to survive the deadlocks that invites.
2. **Commit-adjacent side effects leave a gap:** the message-bus queue release is awaited *after* `Complete()` but *before* disposal — if that publish fails, the DB changes are already permanent and nothing compensates. An at-least-once hole wearing rollback clothing.

To be fair to R2's design: module-owned contexts satisfy the playbook's ownership rule, optimistic-concurrency conflicts return proper problem responses, and non-HTTP components use clean explicit local transactions. But the HTTP glue composes Option 6's commit-by-status-code around Option 7's escalation roulette — inheriting the weaknesses of both while resembling neither.

**Verdict:** avoid in greenfield ASP.NET Core. Justifiable only when wrapping legacy resources (old ADO.NET/COM+/message queue clients) that require ambient enlistment.

---

### The multi-context playbook (N modules ⇒ N DbContexts)

In a modular/DDD codebase each bounded context owns its own `DbContext`, its own migrations and its own transactions. Everything above still applies — **per module**. Four rules keep it coherent:

1. **Ownership:** a `DbContext` belongs to exactly one module. Nothing outside that module references it directly; other layers see only abstractions or integration contracts defined in Application.
2. **One commit per use case, in exactly one context.** A handler may read other modules' data only through published read models/events — never by querying their context. If a use case seems to need writes in two contexts, it is actually two use cases coordinated by events (or your module boundaries are wrong).
3. **No ambient transactions across contexts.** Wrapping two contexts in a `TransactionScope` creates a distributed transaction (the Option 7 trap): lock windows stretch across modules, most managed Postgres/MySQL providers don't support it, and it quietly reintroduces the shared-database coupling you split modules to avoid. The replacement is rule 4.
4. **Outbox lives inside each module's own database**, dispatched by a worker, so "state change + event" is always one local atomic transaction.

What a cross-module workflow looks like end-to-end:

```text
Bookings module                       Payments module
────────────────                      ────────────────
BookTicketCommand
  tx#1: INSERT booking               ← consumes "booking.created"
        + outbox(booking.created)      ChargeCardCommand
                                       tx#2: INSERT payment
                                     + outbox(payment.confirmed)
MarkPaidCommand                      ← consumes "payment.confirmed"
  tx#3: UPDATE booking.status
```

Three local transactions, zero distributed ones, each independently retryable — that is the whole trick.

---

### Microservices: which option wins there?

The options above were described for monolith/modular monolith, but microservices neither add a new kind of option nor remove one — they **collapse the choice**:

- **Inside each service: Options 3 + 4, unchanged.** Each service is one bounded context owning its own `DbContext` and its own database; commands still get exactly one commit point, enforced by the pipeline behavior. If one service feels it needs two internal contexts, question its boundaries first.
- **Between services: Option 5 stops being an optimization and becomes *the architecture*.** Database-per-service means a cross-service transaction cannot exist even in principle — 2PC/distributed transactions are effectively dead in modern practice — so every workflow spanning services is a **saga** (choreographed or orchestrated) built on outbox + message broker.

What changes versus the modular-monolith playbook is mostly transport and failure semantics, not the unit-of-work model:

| Concern | Modular monolith | Microservices |
|---|---|---|
| Event transport | in-process dispatcher / Worker over shared infra | message broker (RabbitMQ / Kafka / Azure Service Bus) |
| Delivery guarantees | effectively once | at-least-once → consumers **must be idempotent** |
| Workflow failures | local retry, rarely visible | saga compensation ("cancel booking when payment fails") |
| Consistency visibility | usually hidden from users | often user-visible → APIs designed for pending states (`202 Accepted` + status endpoint) |

What does **not** change: ad-hoc saves are still broken; a global UoW goes from anti-pattern to *physical impossibility* (no shared process, no shared database); optimistic concurrency remains per-aggregate inside each service; and the intra-service invariant stays *one commit point per command*.

**Stated plainly — go-to baseline for microservices:** Options 3+4 inside every service, Option 5 (outbox + sagas) as the inter-service contract. Nothing else survives contact with distributed reality.

---

## 2. Master comparison

Scores 1–100. Popularity figures are rough estimates of production ASP.NET + EF Core codebases (styles overlap, so they don't sum to 100).

| Criterion | 1 · Ad-hoc saves | 2 · Global UoW | 3 · Scoped + explicit save | 4 · Auto-commit behavior | 5 · Modules + outbox | 6 · Tx per request | 7 · `TransactionScope` |
|---|---:|---:|---:|---:|---:|---:|---:|
| Simplicity | 65 | 35 | 90 | 75 | 45 | 80 | 55 |
| Correctness / atomicity | 25 | 55 | 80 | 92 | 85 | 70 | 50 |
| Testability | 30 | 45 | 88 | 82 | 78 | 75 | 45 |
| Performance | 45 | 50 | 85 | 83 | 92 | 60 | 35 |
| Maintainability (long-term) | 20 | 35 | 85 | 88 | 72 | 65 | 40 |
| Clean-architecture fit | 25 | 40 | 85 | 90 | 95 | 60 | 30 |
| Concurrency safety | 20 | 45 | 70 | 72 | 82 | 65 | 50 |
| Developer experience | 40 | 45 | 88 | 87 | 55 | 78 | 45 |
| **Average** | **34** | **44** | **84** | **84** | **76** | **69** | **44** |
| Popularity | ~30% | ~18% | ~28% | ~14% | ~10% | ~12% | ~7% |

- **Options 6 and 7 are the "legacy defaults":** per-request filter transactions are still a respectable choice for simple MVC apps; ambient `TransactionScope` is the mainframe-era relic to retire on contact.

Reading the table:

- **Option 1 is popular precisely because it's easy until it isn't** — its scores collapse exactly on the criteria that matter most (correctness, concurrency).
- **Option 2 loses on almost everything**: it's abstraction for abstraction's sake over a framework feature that already exists.
- **Options 3+4 tie at 84 while covering each other's weaknesses** (3 lacks enforcement, 4 has slightly more moving parts). Together they're the sweet spot.
- **Option 5 wins on architecture fit and throughput** but costs eventual consistency — deploy it at boundaries, not everywhere.

---

## 3. The other "concurrency": lost updates between transactions

No unit-of-work arrangement protects two simultaneous requests from this race:

1. Request A reads `SeatsAvailable = 1`
2. Request B reads `SeatsAvailable = 1`
3. A saves `-1` → `0`; B saves `-1` → `0` — **one seat sold twice**, no exception anywhere.

The fix is **optimistic concurrency control**: a version token on hot aggregates; EF rejects the second write.

```csharp
public class Event
{
    public Guid Id { get; set; }

    [Timestamp]                        // SQL Server rowversion; Postgres: UseXminAsConcurrencyToken()
    public byte[] Version { get; set; } = default!;
}
```

```csharp
try
{
    await db.SaveChangesAsync(ct);     // second concurrent writer throws here
}
catch (DbUpdateConcurrencyException)
{
    throw new ConflictException("Event changed meanwhile — please retry.");
}
```

This converts silent corruption into a detectable conflict (map to HTTP 409 / retry). For extreme hot spots (single counter row hammered by hundreds of requests), prefer making the decrement itself atomic — `UPDATE ... SET SeatsAvailable -= @n WHERE Id = @id AND SeatsAvailable >= @n` — or serializable isolation scoped to that one command. Bigger/longer transactions are *never* the fix. In a multi-module system these tokens live on aggregates inside their **owning** context; cross-module write races don't exist by design, because modules never write each other's tables.

---

## 4. Decision guide

- **Inside a use case:** Option 3 as the baseline — one commit point in that use case's own module context; Option 4's behavior to enforce it mechanically; extra `SaveChanges` calls allowed when genuinely needed, but always inside the behavior's transaction. Never let a mid-command save commit alone.
- **Between modules:** Option 5 — separate contexts, outbox + events, eventual consistency. Never share a transaction across a module boundary.
- **Between concurrent writers:** optimistic concurrency tokens on contested aggregates; atomic SQL updates for hot counters.
- **Isolation levels:** stay on the DB default (READ COMMITTED) for everything; escalate to REPEATABLE READ/SERIALIZABLE per command only when a business rule genuinely spans multiple statements, and always pair with a retry policy.
- **Microservices:** nothing new — Options 3+4 inside each service, Option 5's outbox + sagas between services. Database-per-service makes cross-service transactions physically impossible, which conveniently removes the temptation.
- **External side effects** (payments, emails): out of the handler's transaction; trigger via after-commit events/outbox so a rollback can't strand them.
- **Never:** `IUnitOfWork` wrapping `DbContext`, repositories that merely re-expose `DbSet`, a UoW injected into every module "so they share transactions," or `TransactionScope` spanning two module contexts (Option 7).

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
