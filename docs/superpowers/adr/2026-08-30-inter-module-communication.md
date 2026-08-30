# ADR: Inter-Module Communication

**Date:** 2026-08-30
**Status:** Approved

## Context

Modules need to communicate without creating circular dependencies or leaking internal details. The codebase has one cross-module edge: Negotiations reads product prices from Catalog.

## Decision

Provider-owned Ports & Adapters pattern.

### Rule 1: Provider owns the port

The module that provides the capability defines the interface and DTOs. The consumer depends only on the public contract.

```csharp
// Catalog/Ports/IProductPriceProvider.cs
namespace PriceNegotiationApp.Modules.Catalog.Ports;

public interface IProductPriceProvider
{
    Task<ProductSnapshot?> GetAsync(Guid productId, CancellationToken ct);
}

public readonly record struct ProductSnapshot(Guid ProductId, decimal Price);
```

### Rule 2: Adapter lives in the provider

The adapter implements the port using the provider's own DbContext. No other module sees the provider's persistence details.

```csharp
// Catalog/Adapters/ProductPriceProvider.cs
namespace PriceNegotiationApp.Modules.Catalog.Adapters;

internal sealed class ProductPriceProvider(CatalogDbContext db) : IProductPriceProvider
{
    public async Task<ProductSnapshot?> GetAsync(Guid productId, CancellationToken ct) =>
        await db.Products.AsNoTracking()
            .Where(p => p.Id == ProductId.From(productId))
            .Select(p => new ProductSnapshot(productId, p.Price))
            .FirstOrDefaultAsync(ct);
}
```

### Rule 3: Host wires the adapter

The host registers the adapter behind the port interface. Modules never reference each other's adapters.

```csharp
// WebApplicationBuilderExtensions.cs
builder.Services.AddScoped<IProductPriceProvider, ProductPriceProvider>();
```

### Rule 4: Consumer sees only contracts

Consumer depends on provider's public `Ports` namespace only. Forbidden: Domain, Persistence, Features, Seeding namespaces.

```csharp
// CreateNegotiationHandler.cs
using PriceNegotiationApp.Modules.Catalog.Ports; // Allowed
// using PriceNegotiationApp.Modules.Catalog.Domain; // Forbidden
// using PriceNegotiationApp.Modules.Catalog.Persistence; // Forbidden
```

## Architecture enforcement

Test: `Negotiations_module_depends_on_catalog_ports_only`

```
Blocks:   Identity, CompositionRoot, Catalog.Domain, Catalog.Persistence,
          Catalog.Features, Catalog.Seeding
Allows:   Catalog.Ports
```

## Adding a new cross-module edge

1. Provider module defines port in `Ports/`
2. Provider module implements adapter in `Adapters/`
3. Consumer module references provider's `Ports` namespace
4. Host registers adapter in `WebApplicationBuilderExtensions.cs`
5. Update architecture test to allow the new dependency

## Files

| File | Role |
|------|------|
| `Catalog/Ports/IProductPriceProvider.cs` | Port interface + DTO |
| `Catalog/Adapters/ProductPriceProvider.cs` | Adapter (reads CatalogDbContext) |
| `Modules.Negotiations.csproj` | References Catalog module |
| `Catalog.csproj` | `InternalsVisibleTo` includes Negotiations |
| `WebApplicationBuilderExtensions.cs` | Wires adapter behind port |
| `ArchitectureShould.cs` | Enforces dependency rules |
