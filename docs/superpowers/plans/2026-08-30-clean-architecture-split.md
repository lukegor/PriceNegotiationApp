# Clean Architecture Split — Multi-Project per Module

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Split each module into 4 projects (Domain, Application, Infrastructure, Contracts) to enforce Clean Architecture boundaries at compile time.

**Architecture:** Each module's files are distributed across 4 projects. Compile-time enforcement via project references. ArchUnitNET tests updated to reflect new project structure.

**Tech Stack:** .NET 10, .slnx solution format, ArchUnitNET, FluentValidation

## Global Constraints

- Assembly names follow pattern: `PriceNegotiationApp.Modules.{Module}.{Layer}`
- All `InternalsVisibleTo` must be updated to reference new assembly names
- All `using` directives must be updated when namespaces change
- Migration files stay in Infrastructure (they depend on DbContext)
- Build must pass with 0 errors, 0 warnings before committing
- All 124 tests must pass

## File Structure

### New Projects (12 total)

| Project | Assembly Name |
|---------|---------------|
| `PriceNegotiationApp.Modules.Catalog.Domain` | `PriceNegotiationApp.Modules.Catalog.Domain` |
| `PriceNegotiationApp.Modules.Catalog.Application` | `PriceNegotiationApp.Modules.Catalog.Application` |
| `PriceNegotiationApp.Modules.Catalog.Infrastructure` | `PriceNegotiationApp.Modules.Catalog.Infrastructure` |
| `PriceNegotiationApp.Modules.Catalog.Contracts` | `PriceNegotiationApp.Modules.Catalog.Contracts` |
| `PriceNegotiationApp.Modules.Negotiations.Domain` | `PriceNegotiationApp.Modules.Negotiations.Domain` |
| `PriceNegotiationApp.Modules.Negotiations.Application` | `PriceNegotiationApp.Modules.Negotiations.Application` |
| `PriceNegotiationApp.Modules.Negotiations.Infrastructure` | `PriceNegotiationApp.Modules.Negotiations.Infrastructure` |
| `PriceNegotiationApp.Modules.Negotiations.Contracts` | `PriceNegotiationApp.Modules.Negotiations.Contracts` |
| `PriceNegotiationApp.Modules.Identity.Domain` | `PriceNegotiationApp.Modules.Identity.Domain` |
| `PriceNegotiationApp.Modules.Identity.Application` | `PriceNegotiationApp.Modules.Identity.Application` |
| `PriceNegotiationApp.Modules.Identity.Infrastructure` | `PriceNegotiationApp.Modules.Identity.Infrastructure` |
| `PriceNegotiationApp.Modules.Identity.Contracts` | `PriceNegotiationApp.Modules.Identity.Contracts` |

### Files to Delete (after split)

| File | Reason |
|------|--------|
| `src/Modules/PriceNegotiationApp.Modules.Catalog/` | Replaced by 4 projects |
| `src/Modules/PriceNegotiationApp.Modules.Negotiations/` | Replaced by 4 projects |
| `src/Modules/PriceNegotiationApp.Modules.Identity/` | Replaced by 4 projects |

---

### Task 1: Create Catalog module projects and move files

**Files to create:**
- `src/Modules/PriceNegotiationApp.Modules.Catalog.Domain/`
- `src/Modules/PriceNegotiationApp.Modules.Catalog.Application/`
- `src/Modules/PriceNegotiationApp.Modules.Catalog.Infrastructure/`
- `src/Modules/PriceNegotiationApp.Modules.Catalog.Contracts/`

**Steps:**

- [ ] **Step 1: Create Catalog.Domain project**

```bash
mkdir -p src/Modules/PriceNegotiationApp.Modules.Catalog.Domain
```

Create `PriceNegotiationApp.Modules.Catalog.Domain.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\..\PriceNegotiationApp.SharedKernel\PriceNegotiationApp.SharedKernel.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Vogen" />
  </ItemGroup>
</Project>
```

Move files from `Catalog/Domain/` to `Catalog.Domain/`:
- `Product.cs`
- `ProductId.cs`
- `Price.cs`

- [ ] **Step 2: Create Catalog.Contracts project**

```bash
mkdir -p src/Modules/PriceNegotiationApp.Modules.Catalog.Contracts
```

Create `PriceNegotiationApp.Modules.Catalog.Contracts.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\..\PriceNegotiationApp.SharedKernel\PriceNegotiationApp.SharedKernel.csproj" />
  </ItemGroup>
</Project>
```

Move files:
- `Ports/IProductPriceProvider.cs` → `Catalog.Contracts/IProductPriceProvider.cs`

- [ ] **Step 3: Create Catalog.Application project**

```bash
mkdir -p src/Modules/PriceNegotiationApp.Modules.Catalog.Application
```

Create `PriceNegotiationApp.Modules.Catalog.Application.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\Catalog.Domain\PriceNegotiationApp.Modules.Catalog.Domain.csproj" />
    <ProjectReference Include="..\..\PriceNegotiationApp.SharedKernel\PriceNegotiationApp.SharedKernel.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
  </ItemGroup>
</Project>
```

Move files:
- `Features/Products/ProductModels.cs` → `Catalog.Application/ProductModels.cs`
- `Features/Products/ProductQuery.cs` → `Catalog.Application/ProductQuery.cs`
- `Features/Products/Create/*` → `Catalog.Application/Create/*`
- `Features/Products/Update/*` → `Catalog.Application/Update/*`
- `Features/Products/Delete/*` → `Catalog.Application/Delete/*`
- `Features/Products/Get/*` → `Catalog.Application/Get/*`
- `Features/Products/List/*` → `Catalog.Application/List/*`

- [ ] **Step 4: Create Catalog.Infrastructure project**

```bash
mkdir -p src/Modules/PriceNegotiationApp.Modules.Catalog.Infrastructure
```

Create `PriceNegotiationApp.Modules.Catalog.Infrastructure.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\Catalog.Application\PriceNegotiationApp.Modules.Catalog.Application.csproj" />
    <ProjectReference Include="..\Catalog.Contracts\PriceNegotiationApp.Modules.Catalog.Contracts.csproj" />
    <ProjectReference Include="..\Catalog.Domain\PriceNegotiationApp.Modules.Catalog.Domain.csproj" />
    <ProjectReference Include="..\..\PriceNegotiationApp.SharedKernel\PriceNegotiationApp.SharedKernel.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="EFCore.NamingConventions" />
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="PriceNegotiationApp.Api" />
    <InternalsVisibleTo Include="PriceNegotiationApp.Modules.Catalog.Tests" />
    <InternalsVisibleTo Include="PriceNegotiationApp.Modules.Negotiations.Application" />
  </ItemGroup>
</Project>
```

Move files:
- `Persistence/*` → `Catalog.Infrastructure/Persistence/*`
- `Adapters/ProductPriceProvider.cs` → `Catalog.Infrastructure/ProductPriceProvider.cs`
- `Seeding/*` → `Catalog.Infrastructure/Seeding/*`
- `CatalogModule.cs` → `Catalog.Infrastructure/CatalogModule.cs`

- [ ] **Step 5: Update namespaces in Catalog files**

Update all `namespace` declarations and `using` directives to reflect new project namespaces.

- [ ] **Step 6: Delete old Catalog project**

```bash
rm -rf src/Modules/PriceNegotiationApp.Modules.Catalog
```

- [ ] **Step 7: Build and verify**

```bash
dotnet build
```

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "Split Catalog module into Domain/Application/Infrastructure/Contracts projects"
```

---

### Task 2: Create Negotiations module projects and move files

**Files to create:**
- `src/Modules/PriceNegotiationApp.Modules.Negotiations.Domain/`
- `src/Modules/PriceNegotiationApp.Modules.Negotiations.Application/`
- `src/Modules/PriceNegotiationApp.Modules.Negotiations.Infrastructure/`
- `src/Modules/PriceNegotiationApp.Modules.Negotiations.Contracts/`

**Steps:**

- [ ] **Step 1: Create Negotiations.Domain project**

Create `PriceNegotiationApp.Modules.Negotiations.Domain.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\..\PriceNegotiationApp.SharedKernel\PriceNegotiationApp.SharedKernel.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Vogen" />
  </ItemGroup>
</Project>
```

Move files:
- `Domain/Negotiation.cs`, `NegotiationId.cs`, `NegotiationStatus.cs`, `NegotiationOutcome.cs`
- `Domain/Customer.cs`, `CustomerId.cs`
- `Domain/Price.cs`
- `Domain/INegotiationPolicy.cs`, `DefaultNegotiationPolicy.cs`
- `Domain/ClosedNegotiationException.cs`, `ProposalExceedsLimitException.cs`

- [ ] **Step 2: Create Negotiations.Contracts project**

Create `PriceNegotiationApp.Modules.Negotiations.Contracts.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\..\PriceNegotiationApp.SharedKernel\PriceNegotiationApp.SharedKernel.csproj" />
  </ItemGroup>
</Project>
```

Move files:
- `Features/Negotiations/NegotiationErrorCodes.cs` → `Negotiations.Contracts/NegotiationErrorCodes.cs`

- [ ] **Step 3: Create Negotiations.Application project**

Create `PriceNegotiationApp.Modules.Negotiations.Application.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\Negotiations.Domain\PriceNegotiationApp.Modules.Negotiations.Domain.csproj" />
    <ProjectReference Include="..\Negotiations.Contracts\PriceNegotiationApp.Modules.Negotiations.Contracts.csproj" />
    <ProjectReference Include="..\Catalog.Contracts\PriceNegotiationApp.Modules.Catalog.Contracts.csproj" />
    <ProjectReference Include="..\..\PriceNegotiationApp.SharedKernel\PriceNegotiationApp.SharedKernel.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
  </ItemGroup>
</Project>
```

Move files:
- `Features/Negotiations/NegotiationModels.cs` → `Negotiations.Application/NegotiationModels.cs`
- `Features/Negotiations/Create/*` → `Negotiations.Application/Create/*`
- `Features/Negotiations/CounterPropose/*` → `Negotiations.Application/CounterPropose/*`
- `Features/Negotiations/Accept/*` → `Negotiations.Application/Accept/*`
- `Features/Negotiations/RejectCurrentOffer/*` → `Negotiations.Application/RejectCurrentOffer/*`
- `Features/Negotiations/Withdraw/*` → `Negotiations.Application/Withdraw/*`
- `Features/Negotiations/Get/*` → `Negotiations.Application/Get/*`
- `Features/Negotiations/List/*` → `Negotiations.Application/List/*`
- `Features/Negotiations/ListMine/*` → `Negotiations.Application/ListMine/*`

- [ ] **Step 4: Create Negotiations.Infrastructure project**

Create `PriceNegotiationApp.Modules.Negotiations.Infrastructure.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\Negotiations.Application\PriceNegotiationApp.Modules.Negotiations.Application.csproj" />
    <ProjectReference Include="..\Negotiations.Contracts\PriceNegotiationApp.Modules.Negotiations.Contracts.csproj" />
    <ProjectReference Include="..\Negotiations.Domain\PriceNegotiationApp.Modules.Negotiations.Domain.csproj" />
    <ProjectReference Include="..\..\PriceNegotiationApp.SharedKernel\PriceNegotiationApp.SharedKernel.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="EFCore.NamingConventions" />
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="PriceNegotiationApp.Api" />
    <InternalsVisibleTo Include="PriceNegotiationApp.Modules.Negotiations.Tests" />
  </ItemGroup>
</Project>
```

Move files:
- `Persistence/*` → `Negotiations.Infrastructure/Persistence/*`
- `Features/Negotiations/NegotiationAccess.cs` → `Negotiations.Infrastructure/NegotiationAccess.cs`
- `NegotiationsModule.cs` → `Negotiations.Infrastructure/NegotiationsModule.cs`

- [ ] **Step 5: Update namespaces in Negotiations files**

- [ ] **Step 6: Delete old Negotiations project**

```bash
rm -rf src/Modules/PriceNegotiationApp.Modules.Negotiations
```

- [ ] **Step 7: Build and verify**

```bash
dotnet build
```

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "Split Negotiations module into Domain/Application/Infrastructure/Contracts projects"
```

---

### Task 3: Create Identity module projects and move files

**Files to create:**
- `src/Modules/PriceNegotiationApp.Modules.Identity.Domain/` (empty)
- `src/Modules/PriceNegotiationApp.Modules.Identity.Application/`
- `src/Modules/PriceNegotiationApp.Modules.Identity.Infrastructure/`
- `src/Modules/PriceNegotiationApp.Modules.Identity.Contracts/`

**Steps:**

- [ ] **Step 1: Create Identity.Domain project (empty)**

Create `PriceNegotiationApp.Modules.Identity.Domain.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\..\PriceNegotiationApp.SharedKernel\PriceNegotiationApp.SharedKernel.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create Identity.Contracts project**

Create `PriceNegotiationApp.Modules.Identity.Contracts.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\..\PriceNegotiationApp.SharedKernel\PriceNegotiationApp.SharedKernel.csproj" />
  </ItemGroup>
</Project>
```

Move files:
- `Features/Auth/AuthModels.cs` → `Identity.Contracts/AuthModels.cs`
- `Features/Auth/IdentityErrorCodes.cs` → `Identity.Contracts/IdentityErrorCodes.cs`

- [ ] **Step 3: Create Identity.Application project**

Create `PriceNegotiationApp.Modules.Identity.Application.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\Identity.Domain\PriceNegotiationApp.Modules.Identity.Domain.csproj" />
    <ProjectReference Include="..\Identity.Contracts\PriceNegotiationApp.Modules.Identity.Contracts.csproj" />
    <ProjectReference Include="..\..\PriceNegotiationApp.SharedKernel\PriceNegotiationApp.SharedKernel.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
  </ItemGroup>
</Project>
```

Move files:
- `Features/Auth/Register/*` → `Identity.Application/Register/*`
- `Features/Auth/Login/*` → `Identity.Application/Login/*`

- [ ] **Step 4: Create Identity.Infrastructure project**

Create `PriceNegotiationApp.Modules.Identity.Infrastructure.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\Identity.Application\PriceNegotiationApp.Modules.Identity.Application.csproj" />
    <ProjectReference Include="..\Identity.Contracts\PriceNegotiationApp.Modules.Identity.Contracts.csproj" />
    <ProjectReference Include="..\Identity.Domain\PriceNegotiationApp.Modules.Identity.Domain.csproj" />
    <ProjectReference Include="..\..\PriceNegotiationApp.SharedKernel\PriceNegotiationApp.SharedKernel.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="EFCore.NamingConventions" />
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />
    <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
    <PackageReference Include="System.IdentityModel.Tokens.Jwt" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="PriceNegotiationApp.Api" />
    <InternalsVisibleTo Include="PriceNegotiationApp.Modules.Identity.Tests" />
  </ItemGroup>
</Project>
```

Move files:
- `Persistence/*` → `Identity.Infrastructure/Persistence/*`
- `Features/Auth/JwtManager.cs` → `Identity.Infrastructure/JwtManager.cs`
- `Features/Auth/EcSigningKey.cs` → `Identity.Infrastructure/EcSigningKey.cs`
- `Features/Auth/JwtOptions.cs` → `Identity.Infrastructure/JwtOptions.cs`
- `Features/Auth/JwtOptionsValidator.cs` → `Identity.Infrastructure/JwtOptionsValidator.cs`
- `Seeding/*` → `Identity.Infrastructure/Seeding/*`
- `IdentityModule.cs` → `Identity.Infrastructure/IdentityModule.cs`

- [ ] **Step 5: Update namespaces in Identity files**

- [ ] **Step 6: Delete old Identity project**

```bash
rm -rf src/Modules/PriceNegotiationApp.Modules.Identity
```

- [ ] **Step 7: Build and verify**

```bash
dotnet build
```

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "Split Identity module into Domain/Application/Infrastructure/Contracts projects"
```

---

### Task 4: Update Api project references

**Files to modify:**
- `src/PriceNegotiationApp.Api/PriceNegotiationApp.Api.csproj`

**Steps:**

- [ ] **Step 1: Update Api csproj**

Replace module references:
```xml
<!-- Old -->
<ProjectReference Include="..\Modules\PriceNegotiationApp.Modules.Catalog\..." />
<ProjectReference Include="..\Modules\PriceNegotiationApp.Modules.Identity\..." />
<ProjectReference Include="..\Modules\PriceNegotiationApp.Modules.Negotiations\..." />

<!-- New -->
<ProjectReference Include="..\Modules\PriceNegotiationApp.Modules.Catalog.Infrastructure\..." />
<ProjectReference Include="..\Modules\PriceNegotiationApp.Modules.Catalog.Contracts\..." />
<ProjectReference Include="..\Modules\PriceNegotiationApp.Modules.Identity.Infrastructure\..." />
<ProjectReference Include="..\Modules\PriceNegotiationApp.Modules.Identity.Contracts\..." />
<ProjectReference Include="..\Modules\PriceNegotiationApp.Modules.Negotiations.Infrastructure\..." />
<ProjectReference Include="..\Modules\PriceNegotiationApp.Modules.Negotiations.Contracts\..." />
```

- [ ] **Step 2: Build and verify**

```bash
dotnet build
```

- [ ] **Step 3: Commit**

```bash
git add src/PriceNegotiationApp.Api/
git commit -m "Update Api project references for Clean Architecture split"
```

---

### Task 5: Update solution file

**Files to modify:**
- `PriceNegotiationApp.slnx`

**Steps:**

- [ ] **Step 1: Update slnx**

Replace 3 module project entries with 12 new project entries.

- [ ] **Step 2: Build and verify**

```bash
dotnet build
```

- [ ] **Step 3: Commit**

```bash
git add PriceNegotiationApp.slnx
git commit -m "Update solution file for Clean Architecture split"
```

---

### Task 6: Update test project references

**Files to modify:**
- `tests/PriceNegotiationApp.ArchitectureTests/PriceNegotiationApp.ArchitectureTests.csproj`
- `tests/PriceNegotiationApp.Modules.Catalog.Tests/PriceNegotiationApp.Modules.Catalog.Tests.csproj`
- `tests/PriceNegotiationApp.Modules.Identity.Tests/PriceNegotiationApp.Modules.Identity.Tests.csproj`
- `tests/PriceNegotiationApp.Modules.Negotiations.Tests/PriceNegotiationApp.Modules.Negotiations.Tests.csproj`
- `tests/PriceNegotiationApp.IntegrationTests/PriceNegotiationApp.IntegrationTests.csproj`

**Steps:**

- [ ] **Step 1: Update test csproj references**

Each test project needs references to the appropriate new projects.

- [ ] **Step 2: Update ArchitectureTests**

Update project references to point to new Infrastructure and Contracts projects.

- [ ] **Step 3: Update module test projects**

Each module test project needs references to the new Application, Infrastructure, and Contracts projects.

- [ ] **Step 4: Build and verify**

```bash
dotnet build
```

- [ ] **Step 5: Commit**

```bash
git add tests/
git commit -m "Update test project references for Clean Architecture split"
```

---

### Task 7: Update ArchitectureTests

**Files to modify:**
- `tests/PriceNegotiationApp.ArchitectureTests/ArchitectureShould.cs`

**Steps:**

- [ ] **Step 1: Update architecture test references**

Update namespace references and type references to reflect new assembly names.

- [ ] **Step 2: Update dependency rule assertions**

Update ArchUnitNET rules to enforce:
- Domain → nothing
- Application → Domain
- Contracts → Domain
- Infrastructure → Application + Contracts + Domain
- Other modules → only target module's Contracts

- [ ] **Step 3: Build and run tests**

```bash
dotnet test
```

- [ ] **Step 4: Commit**

```bash
git add tests/PriceNegotiationApp.ArchitectureTests/
git commit -m "Update architecture tests for Clean Architecture split"
```

---

### Task 8: Run full validation

**Steps:**

- [ ] **Step 1: Clean and rebuild**

```bash
dotnet clean
dotnet build
```

- [ ] **Step 2: Run all tests**

```bash
dotnet test
```

Expected: All 124 tests pass.

- [ ] **Step 3: Verify project structure**

```bash
ls src/Modules/
```

Expected: 12 new projects, no old monolithic module projects.

- [ ] **Step 4: Final commit (if any fixes needed)**

```bash
git add -A
git commit -m "Fix any issues from Clean Architecture split"
```
