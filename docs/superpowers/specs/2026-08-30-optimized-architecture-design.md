# Optimized Architecture Design

**Date:** 2026-08-30
**Status:** Approved (v2 — Clean Architecture multi-project per module)

## Design Rationale

Single project per module with folder-based layers is sufficient for small projects. However, this project assumes growth to a large codebase with multiple developers. Multi-project per module provides:

1. **Compile-time boundary enforcement** — Compiler prevents dependency violations, not just ArchUnitNET tests
2. **Clearer code ownership** — Each team owns specific projects, not folders
3. **Better onboarding** — Project structure teaches architecture to new developers
4. **Future-proofing** — Module extraction to services is already prepared

## Project Structure

```
PriceNegotiationApp.sln
├── src/
│   ├── PriceNegotiationApp.SharedKernel/                    # Domain primitives, exceptions, utilities
│   │
│   ├── Modules/
│   │   ├── PriceNegotiationApp.Modules.Catalog.Domain/      # Pure domain: entities, value objects
│   │   ├── PriceNegotiationApp.Modules.Catalog.Application/ # Use cases: handlers, DTOs, validators
│   │   ├── PriceNegotiationApp.Modules.Catalog.Infrastructure/ # Implementation: persistence, adapters, seeding
│   │   ├── PriceNegotiationApp.Modules.Catalog.Contracts/   # Public API: port interfaces, shared DTOs
│   │   │
│   │   ├── PriceNegotiationApp.Modules.Negotiations.Domain/
│   │   ├── PriceNegotiationApp.Modules.Negotiations.Application/
│   │   ├── PriceNegotiationApp.Modules.Negotiations.Infrastructure/
│   │   ├── PriceNegotiationApp.Modules.Negotiations.Contracts/
│   │   │
│   │   ├── PriceNegotiationApp.Modules.Identity.Domain/     # Empty — Identity uses framework entities
│   │   ├── PriceNegotiationApp.Modules.Identity.Application/
│   │   ├── PriceNegotiationApp.Modules.Identity.Infrastructure/
│   │   └── PriceNegotiationApp.Modules.Identity.Contracts/
│   │
│   └── PriceNegotiationApp.Api/                             # Shared host (composition root)
│       ├── Endpoints/
│       │   ├── Catalog/
│       │   ├── Negotiations/
│       │   └── Identity/
│       ├── Extensions/
│       ├── Composition/
│       ├── ValidateRequestFilter.cs
│       └── GlobalExceptionHandler.cs
│
└── tests/
    ├── PriceNegotiationApp.ArchitectureTests/
    ├── PriceNegotiationApp.IntegrationTests/
    ├── PriceNegotiationApp.Modules.Catalog.Tests/
    ├── PriceNegotiationApp.Modules.Negotiations.Tests/
    ├── PriceNegotiationApp.Modules.Identity.Tests/
    └── PriceNegotiationApp.TestKit/
```

## Layer Responsibilities

| Layer | Project | Contains | Dependencies |
|-------|---------|----------|--------------|
| **Domain** | `*.Domain` | Entities, value objects, domain events, domain interfaces, domain exceptions | SharedKernel only |
| **Application** | `*.Application` | Handlers, request/response DTOs, validators, use-case logic | Domain |
| **Infrastructure** | `*.Infrastructure` | DbContext, EF configs, migrations, adapters, seeding, external services, module composition root | Application + Domain + Contracts |
| **Contracts** | `*.Contracts` | Port interfaces, shared DTOs, error codes (public API surface) | Domain (minimal) |

## Dependency Rules

```
SharedKernel → nothing (pure domain primitives)

Module.Domain → SharedKernel
Module.Application → Module.Domain, SharedKernel
Module.Contracts → Module.Domain, SharedKernel
Module.Infrastructure → Module.Application, Module.Contracts, Module.Domain, SharedKernel

Host → all Modules.Infrastructure, all Modules.Contracts, SharedKernel
Other Modules → only target Module.Contracts
Tests → target Module + Host
```

**Enforcement:** ArchUnitNET tests verify all dependency rules at build time. Compiler enforces project references.

## Module File Mapping

### Catalog Module

| File | Layer | Project |
|------|-------|---------|
| `Product.cs`, `ProductId.cs`, `Price.cs` | Domain | Catalog.Domain |
| `ProductModels.cs`, `ProductQuery.cs` | Application | Catalog.Application |
| `Create/CreateProductHandler.cs`, `Create/CreateProductRequest.cs`, `Create/CreateProductRequestValidator.cs` | Application | Catalog.Application |
| `Update/UpdateProductHandler.cs`, `Update/UpdateProductRequest.cs`, `Update/UpdateProductRequestValidator.cs` | Application | Catalog.Application |
| `Delete/DeleteProductHandler.cs` | Application | Catalog.Application |
| `Get/GetProductHandler.cs` | Application | Catalog.Application |
| `List/ListProductsHandler.cs` | Application | Catalog.Application |
| `CatalogDbContext.cs`, `DesignTimeDbContextFactory.cs` | Infrastructure | Catalog.Infrastructure |
| `Configurations/ProductConfiguration.cs` | Infrastructure | Catalog.Infrastructure |
| `Migrations/*` | Infrastructure | Catalog.Infrastructure |
| `ProductPriceProvider.cs` | Infrastructure | Catalog.Infrastructure |
| `Seeding/*` | Infrastructure | Catalog.Infrastructure |
| `CatalogModule.cs` | Infrastructure | Catalog.Infrastructure |
| `IProductPriceProvider.cs`, `ProductSnapshot` | Contracts | Catalog.Contracts |

### Negotiations Module

| File | Layer | Project |
|------|-------|---------|
| `Negotiation.cs`, `NegotiationId.cs`, `NegotiationStatus.cs`, `NegotiationOutcome.cs` | Domain | Negotiations.Domain |
| `Customer.cs`, `CustomerId.cs` | Domain | Negotiations.Domain |
| `Price.cs` | Domain | Negotiations.Domain |
| `INegotiationPolicy.cs`, `DefaultNegotiationPolicy.cs` | Domain | Negotiations.Domain |
| `ClosedNegotiationException.cs`, `ProposalExceedsLimitException.cs` | Domain | Negotiations.Domain |
| `NegotiationModels.cs` | Application | Negotiations.Application |
| `Create/CreateNegotiationHandler.cs`, `Create/CreateNegotiationRequest.cs`, `Create/CreateNegotiationRequestValidator.cs` | Application | Negotiations.Application |
| `CounterPropose/*` | Application | Negotiations.Application |
| `Accept/AcceptHandler.cs` | Application | Negotiations.Application |
| `RejectCurrentOffer/RejectCurrentOfferHandler.cs` | Application | Negotiations.Application |
| `Withdraw/WithdrawHandler.cs` | Application | Negotiations.Application |
| `Get/GetNegotiationHandler.cs` | Application | Negotiations.Application |
| `List/ListNegotiationsHandler.cs` | Application | Negotiations.Application |
| `ListMine/ListMyNegotiationsHandler.cs` | Application | Negotiations.Application |
| `NegotiationsDbContext.cs`, `DesignTimeDbContextFactory.cs` | Infrastructure | Negotiations.Infrastructure |
| `Configurations/*` | Infrastructure | Negotiations.Infrastructure |
| `Migrations/*` | Infrastructure | Negotiations.Infrastructure |
| `NegotiationAccess.cs` | Infrastructure | Negotiations.Infrastructure |
| `NegotiationsModule.cs` | Infrastructure | Negotiations.Infrastructure |
| `NegotiationErrorCodes.cs` | Contracts | Negotiations.Contracts |

### Identity Module

| File | Layer | Project |
|------|-------|---------|
| _(empty — no pure domain types)_ | Domain | Identity.Domain |
| `AuthModels.cs` | Contracts | Identity.Contracts |
| `IdentityErrorCodes.cs` | Contracts | Identity.Contracts |
| `Register/*` | Application | Identity.Application |
| `Login/*` | Application | Identity.Application |
| `IdentityModuleDbContext.cs`, `ApplicationUser.cs`, `DesignTimeDbContextFactory.cs` | Infrastructure | Identity.Infrastructure |
| `Migrations/*` | Infrastructure | Identity.Infrastructure |
| `JwtManager.cs`, `EcSigningKey.cs`, `JwtOptions.cs`, `JwtOptionsValidator.cs` | Infrastructure | Identity.Infrastructure |
| `Seeding/*` | Infrastructure | Identity.Infrastructure |
| `IdentityModule.cs` | Infrastructure | Identity.Infrastructure |

## Inter-Module Communication

**Pattern:** Provider-owned Contracts

| Component | Location | Owner |
|-----------|----------|-------|
| Port interface | `{Provider}.Contracts` | Provider module |
| Port DTOs | `{Provider}.Contracts` | Provider module |
| Adapter | `{Provider}.Infrastructure` | Provider module |
| Consumer dependency | `{Provider}.Contracts` only | Consumer module |
| DI wiring | `WebApplicationBuilderExtensions.cs` | Host |

**Cross-module reference example:**
```
Negotiations.Application → Catalog.Contracts (for IProductPriceProvider, ProductSnapshot)
```

## Request Validation

**Pattern:** FluentValidation with vertical-slice co-location

| Component | Location | Purpose |
|-----------|----------|---------|
| Request DTO | `Application/{Entity}/{UseCase}/` | Defines request shape |
| Validator | `Application/{Entity}/{UseCase}/` | Validates request before handler |
| Handler | `Application/{Entity}/{UseCase}/` | Business logic |
| Response DTO | `Application/{Entity}/{Entity}Models.cs` | Shared response types |

**Registration:** `AddValidatorsFromAssemblyContaining<T>()` in each module's Infrastructure composition root.

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
| Domain entity | `{Name}` | `Product`, `Negotiation` |
| Value object | `{Name}` | `Price`, `ProductId` |
| Domain exception | `{Name}Exception` | `DomainException` |
| DbContext | `{Module}DbContext` | `CatalogDbContext` |

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
- Registers all modules via `AddXxxModule()` from each module's Infrastructure project
- Wires cross-module adapters behind port interfaces
- Maps module handlers to HTTP endpoints
- Handles global exception processing
- Runs database migrations

Host contains zero business logic. Endpoints are thin HTTP mappings to handler calls.
Host references `*.Infrastructure` projects (for DI registration) and `*.Contracts` projects (for types in endpoint signatures).

## Future Considerations

1. **Repository pattern** — Extract `IProductRepository`, `INegotiationRepository` interfaces into Domain/Contracts to remove direct DbContext injection in handlers
2. **Identity.Domain** — Create `IUserContext` domain interface when custom user logic is needed
3. **Module extraction** — If a module needs to become a separate service, the Contracts project becomes the API contract and the Infrastructure project contains all implementation details
