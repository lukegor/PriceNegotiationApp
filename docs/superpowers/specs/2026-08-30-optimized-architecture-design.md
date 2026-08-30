# Optimized Architecture Design

**Date:** 2026-08-30
**Status:** Approved

## Project Structure

```
PriceNegotiationApp.sln
├── src/
│   ├── PriceNegotiationApp.SharedKernel/
│   │   ├── CallerContext.cs
│   │   ├── DbConnections.cs
│   │   ├── DbWriteGuard.cs
│   │   ├── ErrorCodes.cs
│   │   ├── Exceptions.cs
│   │   ├── PagedResult.cs
│   │   ├── PageQuery.cs
│   │   ├── Policies.cs
│   │   ├── UserRoles.cs
│   │   └── ModuleSeedingHostedServiceBase.cs
│   └── Modules/
│       ├── PriceNegotiationApp.Modules.Catalog/
│       │   ├── Domain/
│       │   │   ├── Product.cs
│       │   │   ├── ProductId.cs
│       │   │   └── Price.cs
│       │   ├── Features/
│       │   │   └── Products/
│       │   │       ├── Create/
│       │   │       │   ├── CreateProductRequest.cs
│       │   │       │   ├── CreateProductRequestValidator.cs
│       │   │       │   └── CreateProductHandler.cs
│       │   │       ├── Update/
│       │   │       ├── Delete/
│       │   │       ├── Get/
│       │   │       ├── List/
│       │   │       └── ProductModels.cs
│       │   ├── Persistence/
│       │   │   ├── CatalogDbContext.cs
│       │   │   ├── Configurations/
│       │   │   └── Migrations/
│       │   ├── Ports/
│       │   │   └── IProductPriceProvider.cs
│       │   ├── Adapters/
│       │   │   └── ProductPriceProvider.cs
│       │   ├── Seeding/
│       │   └── CatalogModule.cs
│       ├── PriceNegotiationApp.Modules.Negotiations/
│       │   ├── Domain/
│       │   │   ├── Negotiation.cs
│       │   │   ├── Customer.cs
│       │   │   ├── NegotiationStatus.cs
│       │   │   ├── NegotiationOutcome.cs
│       │   │   └── INegotiationPolicy.cs
│       │   ├── Features/
│       │   │   └── Negotiations/
│       │   │       ├── Create/
│       │   │       ├── CounterPropose/
│       │   │       ├── Accept/
│       │   │       ├── RejectCurrentOffer/
│       │   │       ├── Withdraw/
│       │   │       ├── Get/
│       │   │       ├── List/
│       │   │       ├── ListMine/
│       │   │       ├── NegotiationAccess.cs
│       │   │       └── NegotiationModels.cs
│       │   ├── Persistence/
│       │   ├── Seeding/
│       │   └── NegotiationsModule.cs
│       ├── PriceNegotiationApp.Modules.Identity/
│       │   ├── Domain/
│       │   ├── Features/
│       │   │   └── Auth/
│       │   │       ├── Login/
│       │   │       ├── Register/
│       │   │       └── AuthModels.cs
│       │   ├── Persistence/
│       │   ├── Seeding/
│       │   └── IdentityModule.cs
│       └── PriceNegotiationApp.Api/
│           ├── Endpoints/
│           │   ├── Catalog/
│           │   ├── Negotiations/
│           │   └── Identity/
│           ├── Extensions/
│           ├── Composition/
│           ├── ValidateRequestFilter.cs
│           └── GlobalExceptionHandler.cs
└── tests/
    ├── PriceNegotiationApp.ArchitectureTests/
    ├── PriceNegotiationApp.IntegrationTests/
    ├── PriceNegotiationApp.Modules.Catalog.Tests/
    ├── PriceNegotiationApp.Modules.Negotiations.Tests/
    ├── PriceNegotiationApp.Modules.Identity.Tests/
    └── PriceNegotiationApp.TestKit/
```

## Dependency Rules

```
SharedKernel → nothing (pure domain primitives)

Module.Domain → nothing (pure domain)
Module.Features → Module.Domain
Module.Persistence → Module.Features, Module.Domain
Module.Ports → nothing (pure interfaces + DTOs)
Module.Adapters → Module.Ports, Module.Persistence
Module.Seeding → Module.Persistence

Host → all Modules, SharedKernel
Other Modules → only target Module.Ports
Tests → target Module + Host
```

**Enforcement:** ArchUnitNET tests verify all dependency rules at build time.

## Inter-Module Communication

**Pattern:** Provider-owned Ports & Adapters

| Component | Location | Owner |
|-----------|----------|-------|
| Port interface | `{Provider}/Ports/` | Provider module |
| Port DTOs | `{Provider}/Ports/` | Provider module |
| Adapter | `{Provider}/Adapters/` | Provider module |
| Consumer dependency | `{Provider}.Ports` namespace only | Consumer module |
| DI wiring | `WebApplicationBuilderExtensions.cs` | Host |

**Adding a new edge:**
1. Provider defines port in `Ports/`
2. Provider implements adapter in `Adapters/`
3. Consumer references provider's `Ports` namespace
4. Host registers adapter behind port interface
5. Update architecture test

## Request Validation

**Pattern:** FluentValidation with vertical-slice co-location

| Component | Location | Purpose |
|-----------|----------|---------|
| Request DTO | `Features/{Entity}/{UseCase}/` | Defines request shape |
| Validator | `Features/{Entity}/{UseCase}/` | Validates request before handler |
| Handler | `Features/{Entity}/{UseCase}/` | Business logic |
| Response DTO | `Features/{Entity}/{Entity}Models.cs` | Shared response types |

**Registration:** `AddValidatorsFromAssemblyContaining<T>()` in each module's DI registration.

**Filter:** `ValidateRequestFilter<TRequest>` added to each endpoint via `.AddEndpointFilter<>()`.

**Error format:** 422 ProblemDetails with `code: "validation_failed"` and field-level errors in `extensions.errors`.

## Naming Conventions

| Type | Naming | Example |
|------|--------|---------|
| Request DTO | `{Action}{Entity}Request` | `CreateProductRequest` |
| Response DTO | `{Entity}Response` | `ProductResponse` |
| Action response | `{Action}{Entity}Response` | `CounterProposalResponse` |
| Handler | `{Action}{Entity}Handler` | `CreateProductHandler` |
| Validator | `{Action}{Entity}RequestValidator` | `CreateProductRequestValidator` |
| Endpoint | `{Action}Endpoint` | `CreateEndpoint` |
| Port | `I{Capability}Provider` | `IProductPriceProvider` |
| Adapter | `{Capability}Provider` | `ProductPriceProvider` |

## Error Handling

| Exception | HTTP Status | When |
|-----------|-------------|------|
| `NotFoundException` | 404 | Resource not found |
| `ConflictException` | 409 | State conflict |
| `InvalidRequestException` | 422 | Request validation |
| `UnauthorizedException` | 401 | Authentication failed |
| `ForbiddenAccessException` | 403 | Authorization failed |
| `DomainException` | 422 | Business rule violated |
| `ValueObjectValidationException` | 422 | Value object invalid |
| Validation filter | 422 | DTO validation failed |

All errors return ProblemDetails with `code` extension and optional `errors` dictionary.

## Customer Decision

Stays in Negotiations module. It's a Negotiations-specific reference row that maps Identity users to the Negotiations context. Only used within Negotiations. Never exposed in API responses. Lazily provisioned on first negotiation.

## Host Responsibilities

The shared host is the composition root. It:
- Configures ASP.NET Core pipeline (auth, CORS, rate limiting, health checks)
- Registers all modules via `AddXxxModule()`
- Wires cross-module adapters behind port interfaces
- Maps module handlers to HTTP endpoints
- Handles global exception processing
- Runs database migrations

Host contains zero business logic. Endpoints are thin HTTP mappings to handler calls.
