# Handler Extraction Design — Endpoints Stop Touching Persistence

Date: 2026-08-25
Problem: minimal-API lambdas inject `CatalogDbContext` / `NegotiationsDbContext` directly
(13 route handlers) and Identity endpoints consume `UserManager<ApplicationUser>`
directly (2 more). Transport concerns and persistence/application concerns are fused in
the same lambda body.

---

## 1. Chosen pattern — per-operation handler classes (vertical slices)

Each operation becomes an injectable service; the endpoint becomes a transport adapter.

```csharp
// BEFORE (Create.cs, abridged)
group.MapPost("/", async (CreateNegotiationRequest request, ClaimsPrincipal principal,
        NegotiationsDbContext db, IProductPriceProvider products, INegotiationPolicy policy,
        TimeProvider clock, CancellationToken ct) => { /* query + mutate + save + map */ })
    .RequireRoles(UserRoles.Customer);

// AFTER
internal sealed class CreateNegotiationHandler(
    NegotiationsDbContext db, IProductPriceProvider products,
    INegotiationPolicy policy, TimeProvider clock)
{
    public async Task<NegotiationResponse> HandleAsync(
        CreateNegotiationRequest command, CallerContext caller, CancellationToken ct) { … }
}

// endpoint keeps only transport concerns
group.MapPost("/", async (CreateNegotiationRequest request, ClaimsPrincipal principal,
        CreateNegotiationHandler handler, CancellationToken ct) =>
    TypedResults.Created("/api/v1/negotiations/mine",
        await handler.HandleAsync(request, principal.ToCallerContext(), ct)))
    .RequireRoles(UserRoles.Customer);
```

## 2. Contract rules

1. **Endpoint files keep:** routes/verbs, status-code shaping via `TypedResults`, auth
   roles/attributes, rate-limit & output-cache policies, `ClaimsPrincipal → CallerContext`
   translation, named-route metadata (`GetProductById`), cache-policy attachments.
2. **Handler files own:** queries (incl. `AsNoTracking`, `ILike` filters, sorting),
   tracking semantics, aggregate mutation, `SaveChangesAsync`, uniqueness-violation
   translation via `DbWriteGuard`, response-DTO construction.
3. **Errors:** handlers throw the existing SharedKernel/module exceptions
   (`NotFoundException`, `ConflictException`, `ForbiddenAccessException`,
   `ClosedNegotiationException`, `ValueObjectValidationException`, …). The
   `GlobalExceptionHandler` contract is untouched — zero API-behavior change.
4. **Caller representation:** handlers accept `CallerContext`; `ClaimsPrincipal` never
   crosses the endpoint boundary.
5. **Registration:** `internal sealed` handlers registered `AddScoped` explicitly inside
   each module's `AddXModule` — greppable, no assembly-scanning magic.
6. **Shared helpers survive:** `NegotiationAccess` remains the persistence-aware helper
   consumed by negotiation handlers; `NegotiationResponses.ToResponse` stays the mapper;
   Catalog's static `RequireAsync` / `SearchAsync` fold into their handlers.

## 3. Scope matrix (15 handlers)

| Module | Operation → Handler | Returns |
|---|---|---|
| Negotiations | POST create → `CreateNegotiationHandler` | `NegotiationResponse` |
| | PATCH proposals → `CounterProposeHandler` | `CounterProposalOutcome` |
| | POST accept → `AcceptHandler` | `StaffActionResponse` |
| | POST decline → `RejectCurrentOfferHandler` | `StaffActionResponse` |
| | DELETE (owner withdraw / admin delete) → `WithdrawHandler` | void (endpoint → 204) |
| | GET one → `GetNegotiationHandler` | `NegotiationResponse` |
| | GET list (staff/admin) → `ListNegotiationsHandler` | `PagedResult<NegotiationResponse>` |
| | GET mine → `ListMyNegotiationsHandler` | `PagedResult<NegotiationResponse>` |
| Catalog | POST → `CreateProductHandler` | `ProductResponse` |
| | PUT → `UpdateProductHandler` | `ProductResponse` |
| | DELETE → `DeleteProductHandler` | void → 204 |
| | GET one → `GetProductHandler` (absorbs `RequireAsync`) | `ProductResponse` |
| | GET list → `ListProductsHandler` (absorbs `SearchAsync`, keeps `ProductQuery`) | `PagedResult<ProductResponse>` |
| Identity | POST register → `RegisterUserHandler` (incl. DbWriteGuard race mapping) | `RegistrationResponse` |
| | POST login → `LoginUserHandler` | `AuthResponse` |

Unchanged / out of scope: `Me` endpoint (pure claims projection, no infrastructure),
health endpoints, seeding hosted services, the `Api` composition-root adapter
(`CatalogToNegotiations` — sanctioned edge), all business behavior.

## 4. Enforcement

New ArchUnitNET fact: every type whose name ends with `Endpoints` must not depend on
`Microsoft.EntityFrameworkCore` nor on either module's `.Persistence` namespace.
README *Tactical DDD laws* gains: **"Endpoints are transport adapters; application logic
lives in per-operation handlers (`Features/**/*Handler`)."**

## 5. Testing strategy

The full integration suite (37 tests) is the regression harness — routes, verbs, status
codes, ProblemDetails codes are all pinned there and must not move. No unit tests are
added by this refactor itself; handlers become independently constructible (real DbContext
against Testcontainers) enabling cheap handler-level tests later if a slice grows logic.

## 6. Rejected alternatives

- Repository/UoW interfaces — violates recorded law F-05 and the `Repository*`
  architecture guard; leaks `IQueryable` anyway.
- MediatR/in-box messaging — ceremony without pipeline payoff at 15 operations.
- Keep-as-is — leaves transport/persistence fusion and untestable lambdas.
