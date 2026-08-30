# FluentValidation Integration Design

**Date:** 2026-08-30
**Status:** Approved

## Problem

Request DTO validation was removed with FluentValidation and never replaced. Validation is scattered across domain entities and handlers with no consistent API boundary validation.

## Solution

Reintroduce FluentValidation with vertical-slice co-location, module-owned registration, and a global endpoint filter.

## Architecture

### Validators — co-located in vertical folders

Each use case folder contains its validator alongside its request DTO and handler:

```
Features/Products/Create/
  CreateProductRequest.cs
  CreateProductRequestValidator.cs
  CreateProductHandler.cs
```

### Registration — module-owned assembly scanning

Each module registers its own validators:

```csharp
// CatalogModule.cs
services.AddValidatorsFromAssemblyContaining<CreateProductRequest>();

// NegotiationsModule.cs
services.AddValidatorsFromAssemblyContaining<CreateNegotiationRequest>();

// IdentityModule.cs
services.AddValidatorsFromAssemblyContaining<LoginRequest>();
```

No API-layer coupling to individual validators.

### Global endpoint filter

`ValidateRequestFilter<TRequest>` registered in `PipelineExtensions.cs`:

1. Resolves `IValidator<TRequest>` from DI
2. Calls `ValidateAsync(request)`
3. Invalid → returns 422 ProblemDetails (existing format with `errors` dict)
4. Valid → passes through

No exception thrown for validation failures. Short-circuits the pipeline cleanly.

### Error response format

Matches existing ProblemDetails pattern:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Invalid request",
  "status": 422,
  "extensions": { "code": "validation_failed" },
  "errors": {
    "Name": ["'Name' must not be empty."],
    "Price": ["'Price' must be greater than '0'."]
  }
}
```

## Validators

| DTO | Rules | Messages |
|-----|-------|----------|
| `CreateProductRequest` | `NotEmpty` + `MaximumLength(200)` + `GreaterThan(0)` | defaults |
| `UpdateProductRequest` | `NotEmpty` + `MaximumLength(200)` + `GreaterThan(0)` | defaults |
| `CreateNegotiationRequest` | `NotEmpty` (ProductId) + `GreaterThan(0)` | defaults |
| `CounterProposalRequest` | `GreaterThan(0)` | default |
| `LoginRequest` | `NotEmpty` + `EmailAddress` + `NotEmpty` (password) | defaults |
| `RegisterRequest` | `NotEmpty` + `EmailAddress` + `NotEmpty` + password regex | custom `.WithMessage()` on regex only |

All default messages except password regex (where FluentValidation's default is bad).

## Code Review Fixes

### Fix A — NegotiationErrorCodes XML doc

Restore removed documentation:

```csharp
/// <summary>Machine-readable error codes owned by this feature (frozen contract).</summary>
internal static class NegotiationErrorCodes { ... }
```

### Fix B — Negotiations CreateEndpoint Location header

Replace hard-coded `/api/v1/negotiations/mine` with `CreatedAtRoute`:

```csharp
var response = await handler.HandleAsync(request, principal.ToCallerContext(), ct);
return TypedResults.CreatedAtRoute(response, "GetNegotiationById", new { id = response.Id });
```

Requires adding `.WithName("GetNegotiationById")` to Negotiations `GetEndpoint.cs`.

## Packages

- `FluentValidation`
- `FluentValidation.DependencyInjectionExtensions`

## Files Modified

| File | Change |
|------|--------|
| 3× module `.csproj` | Add FluentValidation packages |
| `CatalogModule.cs` | Add `AddValidatorsFromAssemblyContaining<CreateProductRequest>()` |
| `NegotiationsModule.cs` | Add `AddValidatorsFromAssemblyContaining<CreateNegotiationRequest>()` |
| `IdentityModule.cs` | Add `AddValidatorsFromAssemblyContaining<LoginRequest>()` |
| 6× new validator files | Create validators |
| `PipelineExtensions.cs` | Register `ValidateRequestFilter` globally |
| `ValidateRequestFilter.cs` | New file |
| `NegotiationErrorCodes.cs` | Restore XML doc |
| `Negotiations/GetEndpoint.cs` | Add `.WithName("GetNegotiationById")` |
| `Negotiations/CreateEndpoint.cs` | Use `CreatedAtRoute` |
