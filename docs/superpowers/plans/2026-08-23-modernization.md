# PriceNegotiationApp Full Modernization — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild the solution per `docs/superpowers/specs/2026-08-23-modernization-design.md` — 4 projects, minimal APIs, PostgreSQL + migrations, hardened security, Docker/CI/OTel, full test coverage of the negotiation lifecycle.

**Architecture:** Greenfield-in-place rebuild, bottom-up: Domain → Application → Infrastructure → Api host → endpoint modules → integration tests → platform. Each task ends buildable; the whole app is runnable end-to-end after Task 9.

**Tech Stack:** .NET 10 / C# latest, ASP.NET Core minimal APIs, EF Core 10 + Npgsql 17, ASP.NET Identity, JWT Bearer, Vogen, Serilog, OpenTelemetry, xunit.v3 + NSubstitute + Bogus + Testcontainers + Refit.

## Global Constraints

- `TreatWarningsAsErrors=true`, analyzers-as-errors, `EnforceCodeStyleInBuild` — every commit must build warning-free.
- Target framework `net10.0`, `Nullable=enable`, `ImplicitUsings=enable` everywhere (from `Directory.Build.props`; do not touch).
- Central package management: **all** package versions live only in `Directory.Packages.props`.
- No committed secrets anywhere. JWT secret ≥ 32 chars via user-secrets/env.
- Domain references no packages except Vogen. Application references no ASP.NET packages.
- Time is always injected (`TimeProvider`); never `DateTime.UtcNow` inside domain/application logic.
- All service/repository methods take `CancellationToken ct` last parameter.
- Error contract: ProblemDetails with `"code"` extension property using constants from `Application/Common/ErrorCodes.cs`.
- User-facing messages in English only.
- Spec deviations made during planning (documented here): (1) `Product.Update` returns `bool` instead of throwing on no-op — PUT stays idempotent; (2) client-visible xmin concurrency 409 is not testable through the API (no version token exposure) — xmin kept server-side only; (3) `Serilog.Enrichers.CorrelationId` removed in favor of OTel trace correlation.

## Canonical type map (all tasks reference these)

```
Domain   ns PriceNegotiationApp.Domain
  ValueObjects/Ids/{ProductId,NegotiationId,CustomerId}.cs   [ValueObject<Guid>(Conversions.EfCoreValueConverter)] readonly partial record struct
  ValueObjects/Price.cs                                      [ValueObject<decimal>(Conversions.EfCoreValueConverter)], Validate > 0
  Exceptions/DomainException.cs                              DomainException(string Message) : Exception
  Exceptions/ProposalExceedsLimitException.cs                : DomainException
  Abstractions/{IBusinessRule,Entity}.cs                     Entity.CheckRule(IBusinessRule)
  Policy/INegotiationPolicy.cs                               MaxProposalsPerNegotiation:int, ProposalMultiplierLimit:decimal
  Policy/DefaultNegotiationPolicy.cs                         3 / 2.0m
  Models/Product.cs                                          Id, Name, Price, Version(uint); Create(name, price); Update(name, price):bool
  Models/Negotiation.cs                                      Id, ProductId, CustomerId, BasePrice, CurrentOffer, Status, ProposalsUsed,
                                                             CreatedAtUtc, LastProposalAtUtc, DecidedAtUtc?, Version(uint)
                                                             Start(customerId, product, offer, now, policy); CounterPropose(offer, now, policy):NegotiationOutcome;
                                                             Accept(now); Decline(now); RemainingProposals(policy):int
  Models/NegotiationStatus.cs                                enum { Open=1, Accepted=2, Declined=3 }
  Models/NegotiationOutcome.cs                               enum { CounterProposed=1, AutoRejected=2, NoProposalsRemaining=3 }
  Models/Customer.cs                                         Id, IdentityUserId(Guid unique); Create(identityUserId)

Application ns PriceNegotiationApp.Application
  Common/UserRoles.cs                                        Admin="Admin", Staff="Staff", Customer="Customer"
  Common/ErrorCodes.cs                                       const strings (see Task 3 code)
  Common/PageQuery.cs                                        record(int Page,int PageSize); Normalized => (>=1, 1..100); Skip
  Common/ProductQuery.cs                                     record(string? Search, decimal? MinPrice, decimal? MaxPrice, string? SortBy, bool SortDesc, int Page, int PageSize)
  Common/PagedResult.cs                                      record<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount)
  Common/CallerContext.cs                                    record(Guid UserId,string Email,IReadOnlySet<string> Roles); IsAuthenticated; IsInRole(r); Anonymous
  Exceptions/NotFoundException.cs                            NotFoundException(string entityName, object key); Code = "<entity>_not_found"
  Exceptions/ConflictException.cs                            ConflictException(string Code, string Message)
  Exceptions/ForbiddenAccessException.cs                     ForbiddenException()
  Exceptions/UnauthorizedException.cs                        UnauthorizedException(string Code, string Message)
  Responses/ProductResponse.cs                               (Guid Id, string Name, decimal Price)
  Responses/NegotiationResponse.cs                           (Guid Id, Guid ProductId, decimal BasePrice, decimal CurrentOffer, string Status,
                                                             int ProposalsUsed, int ProposalsRemaining, DateTimeOffset CreatedAtUtc,
                                                             DateTimeOffset LastProposalAtUtc, DateTimeOffset? DecidedAtUtc)
  Responses/CounterProposalOutcome.cs                        record(string Outcome, NegotiationResponse Negotiation)
  Responses/AuthResponse.cs                                  (string AccessToken, DateTimeOffset ExpiresAtUtc, string Email, IReadOnlyList<string> Roles)
  Responses/RegistrationResponse.cs                          (Guid UserId)
  Responses/CurrentUserResponse.cs                           (Guid UserId, string Email, IReadOnlyList<string> Roles)
  Abstractions/IUnitOfWork.cs                                Task<int> SaveChangesAsync(CancellationToken ct)
  Abstractions/IProductRepository.cs                         GetAsync(ProductId,ct):Task<Product?>; Query():IQueryable<Product>; AddAsync(Product,ct); Remove(Product)
  Abstractions/INegotiationRepository.cs                     GetAsync(NegotiationId,ct); Query(); AddAsync(Negotiation,ct);
                                                             FindOpenAsync(ProductId, Guid identityUserId, ct):Task<Negotiation?>; Remove(Negotiation)
  Abstractions/ICustomerRepository.cs                        GetOrCreateAsync(Guid identityUserId, ct):Task<CustomerId>; GetByIdentityAsync(Guid,ct):Task<Customer?>
  Abstractions/IUserAccountStore.cs                          RegistrationOutcome(bool Succeeded, Guid UserId, string? ErrorDescription);
                                                             SignInResultKind { Success, LockedOut, Failure }
                                                             RegisterAsync(email,pwd,ct):Task<RegistrationOutcome>;
                                                             PasswordSignInAsync(email,pwd):Task<SignInResultKind>;
                                                             GetRolesAsync(Guid userId,ct):Task<IReadOnlyList<string>>
  Abstractions/IJwtTokenGenerator.cs                         GenerateAsync(userId,email,roles):Task<(string Token, DateTimeOffset ExpiresAtUtc)>
  Features/Products/IProductService.cs                       ListAsync(ProductQuery,ct); GetAsync(Guid id,ct); CreateAsync(string name,decimal price,ct);
                                                             UpdateAsync(Guid id,string name,decimal price,ct); DeleteAsync(Guid id,ct)
  Features/Negotiations/INegotiationService.cs               CreateAsync(CallerContext, Guid productId, decimal proposedPrice, ct);
                                                             GetAsync(CallerContext, Guid id, ct); ListMineAsync(CallerContext, PageQuery, ct);
                                                             ListAsync(PageQuery, ct); CounterProposeAsync(CallerContext, Guid id, decimal offer, ct):
                                                             Task<CounterProposalOutcome>; AcceptAsync(Guid id, ct); DeclineAsync(Guid id, ct);
                                                             WithdrawAsync(CallerContext, Guid id, ct)
  Features/Auth/IAuthService.cs                              RegisterAsync(email,pwd,ct):Task<RegistrationResponse>;
                                                             LoginAsync(email,pwd,ct):Task<AuthResponse>; CurrentUserAsync(CallerContext):CurrentUserResponse
  DependencyInjection.AddApplicationServices(this IServiceCollection)

Infrastructure ns PriceNegotiationApp.Infrastructure
  Identity/ApplicationUser.cs                                : IdentityUser (no custom members)
  Persistence/AppDbContext.cs                                IdentityDbContext<ApplicationUser,IdentityRole<Guid>,Guid>
  Persistence/DbEntityConfigurations/{Product,Negotiation,Customer}Configuration.cs
  Persistence/Repositories/{ProductRepository,NegotiationRepository,CustomerRepository,UnitOfWork}.cs
  Identity/IdentityAccountStore.cs                           : IUserAccountStore
  Auth/JwtOptions.cs                                         Issuer, Audience, SecretKey, ExpiryMinutes
  Auth/JwtOptionsValidator.cs                                IValidateOptions<JwtOptions>
  Auth/JwtManager.cs                                         : IJwtTokenGenerator
  Seeding/SeedingOptions.cs                                  AdminEmail,AdminPassword,StaffEmail,StaffPassword,SeedSampleProducts:bool
  Seeding/SeedingHostedService.cs                            migrate + roles/users/products seeding
  Data/DesignTimeDbContextFactory.cs                         for dotnet-ef without Api startup
  DependencyInjection.AddInfrastructure(this IServiceCollection, IConfiguration)

Api ns PriceNegotiationApp.Api
  Program.cs                                                 thin composition root (+ public partial class Program)
  Extensions/WebApplicationBuilderExtensions.AddApiServices()
  Extensions/PipelineExtensions.UsePipeline()
  Extensions/ClaimsPrincipalExtensions.ToCallerContext()
  Extensions/EndpointConventionExtensions.RequireRoles<T>()
  GlobalExceptionHandler.cs                                  IExceptionHandler
  Contracts/{AuthRequests,ProductRequests,NegotiationRequests}.cs   DataAnnotations-validated records
  Modules/{AuthModule,ProductsModule,NegotiationsModule}.cs  MapXxxApi(IEndpointRouteBuilder)
```

---

### Task 1: Repo hygiene

**Files:**
- Delete: root `PriceNegotiationApp.{Api,Application,Contracts,Domain,Infrastructure,Presentation,SharedKernel}/` (empty leftovers), `logs/`, `PriceNegotiationApp.Api.json`
- Modify: `.gitignore`

- [ ] **Step 1: Delete cruft**

```pwsh
git rm --cached PriceNegotiationApp.Api.json
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue `
  PriceNegotiationApp.Api, PriceNegotiationApp.Application, PriceNegotiationApp.Contracts, `
  PriceNegotiationApp.Domain, PriceNegotiationApp.Infrastructure, PriceNegotiationApp.Presentation, `
  PriceNegotiationApp.SharedKernel, logs, PriceNegotiationApp.Api.json
```

- [ ] **Step 2: Append to `.gitignore`**

```gitignore
artifacts/
logs/
*.user
```

- [ ] **Step 3: Validate & commit**

```pwsh
git status   # confirm only intended deletions/modifications staged
git add -A && git commit -m "Remove stale artifacts, empty legacy project folders, committed OpenAPI json"
```

---

### Task 2: Package manifest, project graph, source wipe

**Files:**
- Modify: `Directory.Packages.props`, `PriceNegotiationApp.slnx`
- Rewrite: all six `src/**/*.csproj` and both `tests/**/*.csproj`
- Create: `src/PriceNegotiationApp.Api/Program.cs` (stub)
- Delete: `src/PriceNegotiationApp.Contracts/`, `src/PriceNegotiationApp.Presentation/`, `src/PriceNegotiationApp.SharedKernel/` (whole folders), all `*.cs` under `src/PriceNegotiationApp.Application`, `src/PriceNegotiationApp.Infrastructure`, `src/PriceNegotiationApp.Api` (except new stub), all `*.cs` under `tests/`

**Interfaces:**
- Produces: compilable empty solution skeleton that later tasks fill.

- [ ] **Step 1: Rewrite `Directory.Packages.props`**

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>false</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Bogus" Version="35.6.5" />
    <PackageVersion Include="coverlet.collector" Version="10.0.1" />
    <PackageVersion Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.8" />
    <PackageVersion Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.8" />
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.8" />
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.8" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.8" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.8" />
    <PackageVersion Include="Microsoft.Extensions.ApiDescription.Server" Version="10.0.8" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.8" />
    <PackageVersion Include="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore" Version="10.0.8" />
    <PackageVersion Include="Microsoft.Extensions.Hosting.Abstractions" Version="10.0.8" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.5.1" />
    <PackageVersion Include="NSubstitute" Version="5.3.0" />
    <PackageVersion Include="Refit" Version="10.1.6" />
    <PackageVersion Include="Scalar.AspNetCore" Version="2.14.14" />
    <PackageVersion Include="Serilog.AspNetCore" Version="10.0.0" />
    <PackageVersion Include="Serilog.Sinks.Grafana.Loki" Version="8.3.2" />
    <PackageVersion Include="System.IdentityModel.Tokens.Jwt" Version="8.18.0" />
    <PackageVersion Include="Vogen" Version="8.0.5" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5" />
    <PackageVersion Include="xunit.v3" Version="3.2.2" />
  </ItemGroup>
  <ItemGroup>
    <GlobalPackageReference Include="SonarAnalyzer.CSharp" Version="10.26.0.140279" />
    <GlobalPackageReference Include="Meziantou.Analyzer" Version="3.0.89" />
  </ItemGroup>
</Project>
```
(Later tasks add Npgsql/NamingConventions/OpenTelemetry/Testcontainers via `dotnet add package`, which updates this file automatically under CPM.)

- [ ] **Step 2: Rewrite the csproj files**

`src/PriceNegotiationApp.Domain/PriceNegotiationApp.Domain.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Vogen" />
  </ItemGroup>
</Project>
```

`src/PriceNegotiationApp.Application/PriceNegotiationApp.Application.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\PriceNegotiationApp.Domain\PriceNegotiationApp.Domain.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
  </ItemGroup>
</Project>
```

`src/PriceNegotiationApp.Infrastructure/PriceNegotiationApp.Infrastructure.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\PriceNegotiationApp.Application\PriceNegotiationApp.Application.csproj" />
    <ProjectReference Include="..\PriceNegotiationApp.Domain\PriceNegotiationApp.Domain.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" />
    <PackageReference Include="System.IdentityModel.Tokens.Jwt" />
  </ItemGroup>
</Project>
```

`src/PriceNegotiationApp.Api/PriceNegotiationApp.Api.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <InvariantGlobalization>true</InvariantGlobalization>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <OpenApiDocumentsDirectory>$(MSBuildThisFileDirectory)../../artifacts/openapi</OpenApiDocumentsDirectory>
    <OpenApiGenerateDocuments>true</OpenApiGenerateDocuments>
    <OpenApiGenerateDocumentsOnBuild>true</OpenApiGenerateDocumentsOnBuild>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\PriceNegotiationApp.Application\PriceNegotiationApp.Application.csproj" />
    <ProjectReference Include="..\PriceNegotiationApp.Infrastructure\PriceNegotiationApp.Infrastructure.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" />
    <PackageReference Include="Microsoft.Extensions.ApiDescription.Server">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Scalar.AspNetCore" />
    <PackageReference Include="Serilog.AspNetCore" />
    <PackageReference Include="Serilog.Sinks.Grafana.Loki" />
  </ItemGroup>
</Project>
```

`tests/PriceNegotiationApp.UnitTests/PriceNegotiationApp.UnitTests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\..\src\PriceNegotiationApp.Domain\PriceNegotiationApp.Domain.csproj" />
    <ProjectReference Include="..\..\src\PriceNegotiationApp.Application\PriceNegotiationApp.Application.csproj" />
    <ProjectReference Include="..\..\src\PriceNegotiationApp.Infrastructure\PriceNegotiationApp.Infrastructure.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Bogus" />
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="NSubstitute" />
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>
</Project>
```

`tests/PriceNegotiationApp.IntegrationTests/PriceNegotiationApp.IntegrationTests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\..\src\PriceNegotiationApp.Api\PriceNegotiationApp.Api.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Bogus" />
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
    <PackageReference Include="Refit" />
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Wipe old sources, create stub Program**

```pwsh
git rm -r src/PriceNegotiationApp.Contracts src/PriceNegotiationApp.Presentation src/PriceNegotiationApp.SharedKernel
Get-ChildItem -Recurse -Filter *.cs src/PriceNegotiationApp.Application, src/PriceNegotiationApp.Infrastructure | Remove-Item -Force
Get-ChildItem -Recurse -Filter *.cs src/PriceNegotiationApp.Api | Where-Object Name -ne 'Program.cs' | Remove-Item -Force
Get-ChildItem -Recurse -Filter *.cs tests | Remove-Item -Force
```

New `src/PriceNegotiationApp.Api/Program.cs`:
```csharp
var app = WebApplication.Create(args);

app.MapGet("/", () => Results.Ok("PriceNegotiationApp"));

app.Run();
```

- [ ] **Step 4: Update `PriceNegotiationApp.slnx`**

Replace the `/src/` and `/tests/` folder contents so the projects are exactly:

```xml
<Solution>
  <Folder Name="/Solution Items/">
    <File Path="Directory.Build.props" />
    <File Path="Directory.Packages.props" />
  </Folder>
  <Folder Name="/src/">
    <Project Path="src/PriceNegotiationApp.Api/PriceNegotiationApp.Api.csproj" />
    <Project Path="src/PriceNegotiationApp.Application/PriceNegotiationApp.Application.csproj" />
    <Project Path="src/PriceNegotiationApp.Domain/PriceNegotiationApp.Domain.csproj" />
    <Project Path="src/PriceNegotiationApp.Infrastructure/PriceNegotiationApp.Infrastructure.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/PriceNegotiationApp.IntegrationTests/PriceNegotiationApp.IntegrationTests.csproj" />
    <Project Path="tests/PriceNegotiationApp.UnitTests/PriceNegotiationApp.UnitTests.csproj" />
  </Folder>
</Solution>
```

- [ ] **Step 5: Validate & commit**

```pwsh
dotnet restore && dotnet build
dotnet test   # zero tests, must pass trivially
git add -A && git commit -m "Restructure to 4+2 projects, centralize packages, wipe legacy sources"
```

---

### Task 3: Domain layer rebuild + lifecycle unit tests

**Files:**
- Create: all files under `src/PriceNegotiationApp.Domain/` listed in the canonical type map
- Test: `tests/PriceNegotiationApp.UnitTests/Domain/NegotiationLifecycleShould.cs`, `tests/PriceNegotiationApp.UnitTests/Domain/PriceShould.cs`

**Interfaces:**
- Consumes: nothing (leaf layer)
- Produces: full domain surface from the canonical type map — especially `Negotiation.Start/CounterPropose/Accept/Decline/RemainingProposals`, `Product.Create/Update`, `Price.From/Create`.

- [ ] **Step 1: Value objects**

`src/PriceNegotiationApp.Domain/ValueObjects/Ids/ProductId.cs` (and identical shape for `NegotiationId`, `CustomerId`):
```csharp
using Vogen;

namespace PriceNegotiationApp.Domain.ValueObjects.Ids;

[ValueObject<Guid>(Conversions.EfCoreValueConverter)]
public readonly partial record struct ProductId;
```

`src/PriceNegotiationApp.Domain/ValueObjects/Price.cs`:
```csharp
using Vogen;

namespace PriceNegotiationApp.Domain.ValueObjects;

[ValueObject<decimal>(Conversions.EfCoreValueConverter)]
public readonly partial record struct Price
{
    private static Validation Validate(decimal value) =>
        value > 0m ? Validation.Ok : Validation.Invalid("Price must be greater than zero.");
}
```

- [ ] **Step 2: Exceptions, rule abstractions, policy**

`src/PriceNegotiationApp.Domain/Exceptions/DomainException.cs`:
```csharp
namespace PriceNegotiationApp.Domain.Exceptions;

public class DomainException(string message) : Exception(message);
```

`src/PriceNegotiationApp.Domain/Exceptions/ProposalExceedsLimitException.cs`:
```csharp
namespace PriceNegotiationApp.Domain.Exceptions;

public sealed class ProposalExceedsLimitException(decimal limit)
    : DomainException($"Proposal exceeds the allowed limit of {limit}.")
{
    public decimal Limit { get; } = limit;
}
```

`src/PriceNegotiationApp.Domain/Abstractions/IBusinessRule.cs`:
```csharp
namespace PriceNegotiationApp.Domain.Abstractions;

public interface IBusinessRule
{
    bool IsBroken();
    string Message { get; }
}
```

`src/PriceNegotiationApp.Domain/Abstractions/Entity.cs`:
```csharp
using PriceNegotiationApp.Domain.Exceptions;

namespace PriceNegotiationApp.Domain.Abstractions;

public abstract class Entity
{
    protected static void CheckRule(IBusinessRule rule)
    {
        if (rule.IsBroken())
        {
            throw new DomainException(rule.Message);
        }
    }
}
```

`src/PriceNegotiationApp.Domain/Policy/INegotiationPolicy.cs`:
```csharp
namespace PriceNegotiationApp.Domain.Policy;

public interface INegotiationPolicy
{
    int MaxProposalsPerNegotiation { get; }

    decimal ProposalMultiplierLimit { get; }
}
```

`src/PriceNegotiationApp.Domain/Policy/DefaultNegotiationPolicy.cs`:
```csharp
namespace PriceNegotiationApp.Domain.Policy;

public sealed class DefaultNegotiationPolicy : INegotiationPolicy
{
    public int MaxProposalsPerNegotiation => 3;

    public decimal ProposalMultiplierLimit => 2.0m;
}
```

- [ ] **Step 3: Entities**

`src/PriceNegotiationApp.Domain/Models/Rules.cs`:
```csharp
using PriceNegotiationApp.Domain.Abstractions;
using PriceNegotiationApp.Domain.ValueObjects;

namespace PriceNegotiationApp.Domain.Models;

internal sealed record ProductNameMustNotBeEmpty(string? Value) : IBusinessRule
{
    public bool IsBroken() => string.IsNullOrWhiteSpace(Value);

    public string Message => "Product name must not be empty.";
}

internal sealed record NegotiationMustBeOpenRule(NegotiationStatus Status) : IBusinessRule
{
    public bool IsBroken() => Status != NegotiationStatus.Open;

    public string Message => "Negotiation is already closed.";
}
```

`src/PriceNegotiationApp.Domain/Models/Product.cs`:
```csharp
using PriceNegotiationApp.Domain.Abstractions;
using PriceNegotiationApp.Domain.ValueObjects;
using PriceNegotiationApp.Domain.ValueObjects.Ids;

namespace PriceNegotiationApp.Domain.Models;

public sealed class Product : Entity
{
    public ProductId Id { get; private set; }

    public string Name { get; private set; } = null!;

    public Price Price { get; private set; }

    /// <summary>Optimistic-concurrency token mapped to PostgreSQL xmin.</summary>
    public uint Version { get; private set; }

    private Product()
    {
    }

    private Product(ProductId id, string name, Price price)
    {
        CheckRule(new ProductNameMustNotBeEmpty(name));
        Id = id;
        Name = name.Trim();
        Price = price;
    }

    public static Product Create(string name, Price price) =>
        new(ProductId.From(Guid.CreateVersion7()), name, price);

    /// <summary>Applies changes. Returns false when nothing changed (PUT stays idempotent).</summary>
    public bool Update(string name, Price price)
    {
        CheckRule(new ProductNameMustNotBeEmpty(name));
        var trimmed = name.Trim();
        if (Name == trimmed && Price == price)
        {
            return false;
        }

        Name = trimmed;
        Price = price;
        return true;
    }
}
```

`src/PriceNegotiationApp.Domain/Models/NegotiationStatus.cs`:
```csharp
namespace PriceNegotiationApp.Domain.Models;

public enum NegotiationStatus
{
    Open = 1,
    Accepted = 2,
    Declined = 3,
}
```

`src/PriceNegotiationApp.Domain/Models/NegotiationOutcome.cs`:
```csharp
namespace PriceNegotiationApp.Domain.Models;

public enum NegotiationOutcome
{
    CounterProposed = 1,
    AutoRejected = 2,
    NoProposalsRemaining = 3,
}
```

`src/PriceNegotiationApp.Domain/Models/Negotiation.cs`:
```csharp
using PriceNegotiationApp.Domain.Abstractions;
using PriceNegotiationApp.Domain.Exceptions;
using PriceNegotiationApp.Domain.Policy;
using PriceNegotiationApp.Domain.ValueObjects;
using PriceNegotiationApp.Domain.ValueObjects.Ids;

namespace PriceNegotiationApp.Domain.Models;

public sealed class Negotiation : Entity
{
    public NegotiationId Id { get; private set; }

    public ProductId ProductId { get; private set; }

    public CustomerId CustomerId { get; private set; }

    /// <summary>Base price snapshot taken at creation; protects ongoing negotiations from later product price changes.</summary>
    public Price BasePrice { get; private set; }

    public Price CurrentOffer { get; private set; }

    public NegotiationStatus Status { get; private set; }

    /// <summary>Total proposals recorded, including the initial one.</summary>
    public int ProposalsUsed { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset LastProposalAtUtc { get; private set; }

    public DateTimeOffset? DecidedAtUtc { get; private set; }

    public uint Version { get; private set; }

    private Negotiation()
    {
    }

    private Negotiation(
        NegotiationId id, ProductId productId, CustomerId customerId, Price basePrice, Price currentOffer,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        ProductId = productId;
        CustomerId = customerId;
        BasePrice = basePrice;
        CurrentOffer = currentOffer;
        Status = NegotiationStatus.Open;
        ProposalsUsed = 1;
        CreatedAtUtc = createdAtUtc;
        LastProposalAtUtc = createdAtUtc;
    }

    public static Negotiation Start(CustomerId customerId, Product product, Price initialOffer, DateTimeOffset now, INegotiationPolicy policy)
    {
        EnsureWithinLimit(product.Price, initialOffer, policy);
        return new Negotiation(NegotiationId.From(Guid.CreateVersion7()), product.Id, customerId, product.Price, initialOffer, now);
    }

    public NegotiationOutcome CounterPropose(Price offer, DateTimeOffset now, INegotiationPolicy policy)
    {
        CheckRule(new NegotiationMustBeOpenRule(Status));
        if (ProposalsUsed >= policy.MaxProposalsPerNegotiation)
        {
            return NegotiationOutcome.NoProposalsRemaining;
        }

        try
        {
            EnsureWithinLimit(BasePrice, offer, policy);
        }
        catch (ProposalExceedsLimitException)
        {
            Status = NegotiationStatus.Declined;
            DecidedAtUtc = now;
            return NegotiationOutcome.AutoRejected;
        }

        CurrentOffer = offer;
        ProposalsUsed++;
        LastProposalAtUtc = now;
        return NegotiationOutcome.CounterProposed;
    }

    public void Accept(DateTimeOffset now) => Decide(NegotiationStatus.Accepted, now);

    public void Decline(DateTimeOffset now) => Decide(NegotiationStatus.Declined, now);

    public int RemainingProposals(INegotiationPolicy policy) =>
        Math.Max(0, policy.MaxProposalsPerNegotiation - ProposalsUsed);

    private void Decide(NegotiationStatus terminalStatus, DateTimeOffset now)
    {
        CheckRule(new NegotiationMustBeOpenRule(Status));
        Status = terminalStatus;
        DecidedAtUtc = now;
    }

    private static void EnsureWithinLimit(Price basePrice, Price offer, INegotiationPolicy policy)
    {
        var limit = basePrice.Value * policy.ProposalMultiplierLimit;
        if (offer.Value > limit)
        {
            throw new ProposalExceedsLimitException(limit);
        }
    }
}
```

`src/PriceNegotiationApp.Domain/Models/Customer.cs`:
```csharp
using PriceNegotiationApp.Domain.Abstractions;
using PriceNegotiationApp.Domain.ValueObjects.Ids;

namespace PriceNegotiationApp.Domain.Models;

public sealed class Customer : Entity
{
    public CustomerId Id { get; private set; }

    public Guid IdentityUserId { get; private set; }

    private Customer()
    {
    }

    private Customer(CustomerId id, Guid identityUserId)
    {
        Id = id;
        IdentityUserId = identityUserId;
    }

    public static Customer Create(Guid identityUserId) =>
        new(CustomerId.From(Guid.CreateVersion7()), identityUserId);
}
```

- [ ] **Step 4: Unit tests**

`tests/PriceNegotiationApp.UnitTests/Domain/PriceShould.cs`:
```csharp
using PriceNegotiationApp.Domain.Exceptions;
using PriceNegotiationApp.Domain.ValueObjects;

namespace PriceNegotiationApp.UnitTests.Domain;

public class PriceShould
{
    [Fact]
    public void Accept_positive_values()
    {
        var price = Price.From(19.99m);
        Assert.Equal(19.99m, price.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Reject_zero_or_negative_values(decimal value) =>
        Assert.Throws<ValueObjectValidationException>(() => Price.From(value));
}
```

`tests/PriceNegotiationApp.UnitTests/Domain/NegotiationLifecycleShould.cs`:
```csharp
using Bogus;
using PriceNegotiationApp.Domain.Exceptions;
using PriceNegotiationApp.Domain.Models;
using PriceNegotiationApp.Domain.Policy;
using PriceNegotiationApp.Domain.ValueObjects;
using PriceNegotiationApp.Domain.ValueObjects.Ids;

namespace PriceNegotiationApp.UnitTests.Domain;

public class NegotiationLifecycleShould
{
    private static readonly DefaultNegotiationPolicy Policy = new();
    private readonly Faker _faker = new();
    private readonly Product _product = Product.Create("Widget", Price.From(100m));
    private readonly DateTimeOffset _now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private Negotiation StartValid() =>
        Negotiation.Start(CustomerId.From(_faker.Random.Guid()), _product, Price.From(80m), _now, Policy);

    [Fact]
    public void Start_records_initial_proposal_and_consumes_one_of_three_budgets()
    {
        var negotiation = StartValid();

        Assert.Equal(NegotiationStatus.Open, negotiation.Status);
        Assert.Equal(1, negotiation.ProposalsUsed);
        Assert.Equal(100m, negotiation.BasePrice.Value);
        Assert.Equal(2, negotiation.RemainingProposals(Policy));
    }

    [Fact]
    public void Start_rejects_offer_over_twice_base_price()
    {
        var over = Price.From(201m); // > 2 x 100

        Assert.Throws<ProposalExceedsLimitException>(
            () => Negotiation.Start(CustomerId.From(_faker.Random.Guid()), _product, over, _now, Policy));
    }

    [Fact]
    public void Start_accepts_offer_exactly_at_limit()
    {
        var atLimit = Price.From(200m); // == 2 x 100 passes

        var negotiation = Negotiation.Start(CustomerId.From(_faker.Random.Guid()), _product, atLimit, _now, Policy);

        Assert.Equal(200m, negotiation.CurrentOffer.Value);
    }

    [Fact]
    public void CounterPropose_stores_new_offer_within_limit()
    {
        var negotiation = StartValid();

        var outcome = negotiation.CounterPropose(Price.From(90m), _now.AddMinutes(5), Policy);

        Assert.Equal(NegotiationOutcome.CounterProposed, outcome);
        Assert.Equal(90m, negotiation.CurrentOffer.Value);
        Assert.Equal(2, negotiation.ProposalsUsed);
        Assert.Equal(NegotiationStatus.Open, negotiation.Status);
    }

    [Fact]
    public void CounterPropose_over_limit_auto_rejects_and_closes()
    {
        var negotiation = StartValid();

        var outcome = negotiation.CounterPropose(Price.From(500m), _now.AddMinutes(5), Policy);

        Assert.Equal(NegotiationOutcome.AutoRejected, outcome);
        Assert.Equal(NegotiationStatus.Declined, negotiation.Status);
        Assert.NotNull(negotiation.DecidedAtUtc);
    }

    [Fact]
    public void CounterPropose_after_budget_exhaustion_returns_NoProposalsRemaining()
    {
        var negotiation = StartValid();
        negotiation.CounterPropose(Price.From(90m), _now, Policy);
        negotiation.CounterPropose(Price.From(91m), _now, Policy);

        // Used = 3 of 3; further counter-proposals are refused
        var outcome = negotiation.CounterPropose(Price.From(92m), _now, Policy);

        Assert.Equal(NegotiationOutcome.NoProposalsRemaining, outcome);
        Assert.Equal(92m != negotiation.CurrentOffer.Value, true); // offer unchanged
        Assert.Equal(NegotiationStatus.Open, negotiation.Status);
    }

    [Fact]
    public void Accept_closes_negotiation_as_Accepted()
    {
        var negotiation = StartValid();

        negotiation.Accept(_now.AddDays(1));

        Assert.Equal(NegotiationStatus.Accepted, negotiation.Status);
        Assert.NotNull(negotiation.DecidedAtUtc);
    }

    [Fact]
    public void Decline_closes_negotiation_as_Declined()
    {
        var negotiation = StartValid();

        negotiation.Decline(_now.AddDays(1));

        Assert.Equal(NegotiationStatus.Declined, negotiation.Status);
    }

    [Fact]
    public void Terminal_negotiations_refuse_further_operations()
    {
        var negotiation = StartValid();
        negotiation.Accept(_now);

        Assert.Throws<DomainException>(() => negotiation.CounterPropose(Price.From(50m), _now, Policy));
        Assert.Throws<DomainException>(() => negotiation.Accept(_now));
        Assert.Throws<DomainException>(() => negotiation.Decline(_now));
    }
}
```

Also create `tests/PriceNegotiationApp.UnitTests/Domain/ProductRulesShould.cs` covering `Create` rejects null/whitespace name, `Create` trims name, `Update` returns true on change / false when identical, `Update` rejects whitespace. Write it following the same style (plain xUnit asserts, one `Faker` field).

- [ ] **Step 5: Validate & commit**

```pwsh
dotnet build && dotnet test tests/PriceNegotiationApp.UnitTests
git add -A && git commit -m "Rebuild domain: Vogen IDs + Price VO, explicit negotiation lifecycle, policy, unit tests"
```

---

### Task 4: Application layer rebuild + service unit tests

**Files:**
- Create: all files under `src/PriceNegotiationApp.Application/` from the canonical type map
- Test: `tests/PriceNegotiationApp.UnitTests/Application/NegotiationServiceShould.cs`, `.../ProductServiceShould.cs`

**Interfaces:**
- Consumes: Domain types from Task 3.
- Produces: services + ports exactly as in the canonical type map (endpoints in Tasks 7–9 depend on those signatures).

- [ ] **Step 1: Common types**

`src/PriceNegotiationApp.Application/Common/UserRoles.cs`:
```csharp
namespace PriceNegotiationApp.Application.Common;

public static class UserRoles
{
    public const string Admin = "Admin";

    public const string Staff = "Staff";

    public const string Customer = "Customer";
}
```

`src/PriceNegotiationApp.Application/Common/ErrorCodes.cs`:
```csharp
namespace PriceNegotiationApp.Application.Common;

public static class ErrorCodes
{
    public const string ProductNotFound = "product_not_found";
    public const string NegotiationNotFound = "negotiation_not_found";
    public const string NegotiationClosed = "negotiation_closed";
    public const string NegotiationAlreadyOpen = "negotiation_already_open";
    public const string NoProposalsRemaining = "no_proposals_remaining";
    public const string ProposalExceedsLimit = "proposal_exceeds_limit";
    public const string EmailAlreadyRegistered = "email_already_registered";
    public const string InvalidCredentials = "invalid_credentials";
    public const string AccountLocked = "account_locked";
    public const string Forbidden = "forbidden";
    public const string ConcurrencyConflict = "conflict";
    public const string InternalError = "internal_error";
}
```

`src/PriceNegotiationApp.Application/Common/PageQuery.cs`:
```csharp
namespace PriceNegotiationApp.Application.Common;

public sealed record PageQuery(int Page, int PageSize)
{
    public int SafePage => Math.Max(1, Page);

    public int SafePageSize => Math.Clamp(PageSize, 1, 100);

    public int Skip => (SafePage - 1) * SafePageSize;
}
```

`src/PriceNegotiationApp.Application/Common/ProductQuery.cs`:
```csharp
namespace PriceNegotiationApp.Application.Common;

public sealed record ProductQuery(
    string? Search = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? SortBy = null,
    bool SortDesc = false,
    int Page = 1,
    int PageSize = 20);
```

`src/PriceNegotiationApp.Application/Common/PagedResult.cs`:
```csharp
namespace PriceNegotiationApp.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount);
```

`src/PriceNegotiationApp.Application/Common/CallerContext.cs`:
```csharp
namespace PriceNegotiationApp.Application.Common;

public sealed record CallerContext(Guid UserId, string Email, IReadOnlySet<string> Roles)
{
    private static readonly IReadOnlySet<string> EmptyRoles = new HashSet<string>();

    public static readonly CallerContext Anonymous = new(Guid.Empty, string.Empty, EmptyRoles);

    public bool IsAuthenticated => UserId != Guid.Empty;

    public bool IsInRole(string role) => Roles.Contains(role);
}
```

- [ ] **Step 2: Exceptions and responses**

`src/PriceNegotiationApp.Application/Exceptions/NotFoundException.cs`:
```csharp
namespace PriceNegotiationApp.Application.Exceptions;

public sealed class NotFoundException(string entityName, object key)
    : Exception($"{entityName} '{key}' was not found.")
{
    public string Code { get; } = $"{entityName.ToLowerInvariant().Replace(" ", string.Empty)}_not_found";
}
```

`src/PriceNegotiationApp.Application/Exceptions/ConflictException.cs`:
```csharp
namespace PriceNegotiationApp.Application.Exceptions;

public sealed class ConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
```

`src/PriceNegotiationApp.Application/Exceptions/ForbiddenAccessException.cs`:
```csharp
namespace PriceNegotiationApp.Application.Exceptions;

public sealed class ForbiddenAccessException() : Exception("Access to the requested resource is forbidden.");
```

`src/PriceNegotiationApp.Application/Exceptions/UnauthorizedException.cs`:
```csharp
namespace PriceNegotiationApp.Application.Exceptions;

public sealed class UnauthorizedException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
```

Response records (`Responses/`) — plain positional records exactly as declared in the canonical type map, e.g.:
```csharp
namespace PriceNegotiationApp.Application.Responses;

public sealed record ProductResponse(Guid Id, string Name, decimal Price);
```
(and `NegotiationResponse`, `CounterProposalOutcome`, `AuthResponse`, `RegistrationResponse`, `CurrentUserResponse`, plus `PagedResult` already above).

- [ ] **Step 3: Ports (abstractions)**

Exactly the interfaces from the canonical type map. Representative full code for the non-obvious ones:

`src/PriceNegotiationApp.Application/Abstractions/IUserAccountStore.cs`:
```csharp
namespace PriceNegotiationApp.Application.Abstractions;

public enum SignInResultKind
{
    Success,
    LockedOut,
    Failure,
}

public sealed record RegistrationOutcome(bool Succeeded, Guid UserId, string? ErrorDescription);

public interface IUserAccountStore
{
    Task<RegistrationOutcome> RegisterAsync(string email, string password, CancellationToken ct);

    Task<SignInResultKind> PasswordSignInAsync(string email, string password);

    Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken ct);
}
```

`src/PriceNegotiationApp.Application/Abstractions/IJwtTokenGenerator.cs`:
```csharp
namespace PriceNegotiationApp.Application.Abstractions;

public interface IJwtTokenGenerator
{
    Task<(string Token, DateTimeOffset ExpiresAtUtc)> GenerateAsync(
        Guid userId, string email, IReadOnlyCollection<string> roles);
}
```

Repository/unit-of-work/customer ports per canonical map (straightforward signatures).

- [ ] **Step 4: Services**

`src/PriceNegotiationApp.Application/Features/Products/ProductService.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Application.Abstractions;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Application.Exceptions;
using PriceNegotiationApp.Application.Responses;
using PriceNegotiationApp.Domain.Models;
using PriceNegotiationApp.Domain.ValueObjects;
using PriceNegotiationApp.Domain.ValueObjects.Ids;

namespace PriceNegotiationApp.Application.Features.Products;

public sealed class ProductService(IProductRepository products, IUnitOfWork uow) : IProductService
{
    public async Task<PagedResult<ProductResponse>> ListAsync(ProductQuery query, CancellationToken ct)
    {
        var page = new PageQuery(query.Page, query.PageSize);
        var q = products.Query();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            q = q.Where(p => EF.Functions.ILike(p.Name, $"%{query.Search.Trim()}%"));
        }

        if (query.MinPrice.HasValue)
        {
            q = q.Where(p => p.Price.Value >= query.MinPrice.Value);
        }

        if (query.MaxPrice.HasValue)
        {
            q = q.Where(p => p.Price.Value <= query.MaxPrice.Value);
        }

        var sortBy = query.SortBy?.Trim().ToLowerInvariant();
        q = (sortBy, query.SortDesc) switch
        {
            ("price", false) => q.OrderBy(p => p.Price.Value),
            ("price", true) => q.OrderByDescending(p => p.Price.Value),
            (_, false) => q.OrderBy(p => p.Name),
            _ => q.OrderByDescending(p => p.Name),
        };

        var total = await q.LongCountAsync(ct);
        var items = await q
            .Skip(page.Skip).Take(page.SafePageSize)
            .Select(p => new ProductResponse(p.Id.Value, p.Name, p.Price.Value))
            .ToListAsync(ct);

        return new PagedResult<ProductResponse>(items, page.SafePage, page.SafePageSize, total);
    }

    public async Task<ProductResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var product = await products.GetAsync(ProductId.From(id), ct)
                      ?? throw new NotFoundException(nameof(Product), id);
        return new ProductResponse(product.Id.Value, product.Name, product.Price.Value);
    }

    public async Task<ProductResponse> CreateAsync(string name, decimal price, CancellationToken ct)
    {
        var product = Product.Create(name, Price.From(price));
        await products.AddAsync(product, ct);
        await uow.SaveChangesAsync(ct);
        return new ProductResponse(product.Id.Value, product.Name, product.Price.Value);
    }

    public async Task<ProductResponse> UpdateAsync(Guid id, string name, decimal price, CancellationToken ct)
    {
        var product = await products.GetAsync(ProductId.From(id), ct)
                      ?? throw new NotFoundException(nameof(Product), id);
        product.Update(name, Price.From(price));
        await uow.SaveChangesAsync(ct);
        return new ProductResponse(product.Id.Value, product.Name, product.Price.Value);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var product = await products.GetAsync(ProductId.From(id), ct)
                      ?? throw new NotFoundException(nameof(Product), id);
        products.Remove(product);
        await uow.SaveChangesAsync(ct);
    }
}
```

`src/PriceNegotiationApp.Application/Features/Negotiations/NegotiationService.cs`:
```csharp
using PriceNegotiationApp.Application.Abstractions;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Application.Exceptions;
using PriceNegotiationApp.Application.Responses;
using PriceNegotiationApp.Domain.Models;
using PriceNegotiationApp.Domain.Policy;
using PriceNegotiationApp.Domain.ValueObjects;
using PriceNegotiationApp.Domain.ValueObjects.Ids;

namespace PriceNegotiationApp.Application.Features.Negotiations;

public sealed class NegotiationService(
    INegotiationRepository negotiations,
    IProductRepository products,
    ICustomerRepository customers,
    INegotiationPolicy policy,
    IUnitOfWork uow,
    TimeProvider time) : INegotiationService
{
    public async Task<NegotiationResponse> CreateAsync(CallerContext caller, Guid productId, decimal proposedPrice, CancellationToken ct)
    {
        var product = await products.GetAsync(ProductId.From(productId), ct)
                      ?? throw new NotFoundException(nameof(Product), productId);

        if (await negotiations.FindOpenAsync(product.Id, caller.UserId, ct) is not null)
        {
            throw new ConflictException(ErrorCodes.NegotiationAlreadyOpen, "An open negotiation already exists for this product.");
        }

        var customerId = await customers.GetOrCreateAsync(caller.UserId, ct);
        var negotiation = Negotiation.Start(customerId, product, Price.From(proposedPrice), time.GetUtcNow(), policy);
        await negotiations.AddAsync(negotiation, ct);
        await uow.SaveChangesAsync(ct);
        return Map(negotiation);
    }

    public async Task<NegotiationResponse> GetAsync(CallerContext caller, Guid id, CancellationToken ct)
    {
        var negotiation = await RequireAccessibleAsync(caller, id, ct);
        return Map(negotiation);
    }

    public async Task<PagedResult<NegotiationResponse>> ListMineAsync(CallerContext caller, PageQuery page, CancellationToken ct)
    {
        var customer = await customers.GetByIdentityAsync(caller.UserId, ct);
        var q = negotiations.Query().Where(n => customer != null && n.CustomerId == customer.Id);
        return await ToPagedAsync(q, page, ct);
    }

    public async Task<PagedResult<NegotiationResponse>> ListAsync(PageQuery page, CancellationToken ct) =>
        await ToPagedAsync(negotiations.Query(), page, ct);

    public async Task<CounterProposalOutcome> CounterProposeAsync(CallerContext caller, Guid id, decimal proposedPrice, CancellationToken ct)
    {
        var negotiation = await RequireOwnerAsync(caller, id, ct);

        var outcome = negotiation.CounterPropose(Price.From(proposedPrice), time.GetUtcNow(), policy);
        switch (outcome)
        {
            case NegotiationOutcome.NoProposalsRemaining:
                throw new ConflictException(ErrorCodes.NoProposalsRemaining, "No proposals remain for this negotiation.");
            case NegotiationOutcome.CounterProposed or NegotiationOutcome.AutoRejected:
                break;
        }

        await uow.SaveChangesAsync(ct);
        return new CounterProposalOutcome(outcome.ToString(), Map(negotiation));
    }

    public async Task<NegotiationResponse> AcceptAsync(Guid id, CancellationToken ct)
    {
        var negotiation = await RequireAsync(id, ct);
        negotiation.Accept(time.GetUtcNow());
        await uow.SaveChangesAsync(ct);
        return Map(negotiation);
    }

    public async Task<NegotiationResponse> DeclineAsync(Guid id, CancellationToken ct)
    {
        var negotiation = await RequireAsync(id, ct);
        negotiation.Decline(time.GetUtcNow());
        await uow.SaveChangesAsync(ct);
        return Map(negotiation);
    }

    public async Task WithdrawAsync(CallerContext caller, Guid id, CancellationToken ct)
    {
        var negotiation = await RequireAsync(id, ct);
        if (!caller.IsInRole(UserRoles.Admin) && !await IsOwnerAsync(caller, negotiation, ct))
        {
            throw new ForbiddenAccessException();
        }

        negotiations.Remove(negotiation);
        await uow.SaveChangesAsync(ct);
    }

    private async Task<Negotiation> RequireAsync(Guid id, CancellationToken ct) =>
        await negotiations.GetAsync(NegotiationId.From(id), ct)
        ?? throw new NotFoundException(nameof(Negotiation), id);

    private async Task<Negotiation> RequireOwnerAsync(CallerContext caller, Guid id, CancellationToken ct)
    {
        var negotiation = await RequireAsync(id, ct);
        if (!await IsOwnerAsync(caller, negotiation, ct))
        {
            throw new ForbiddenAccessException();
        }

        return negotiation;
    }

    private async Task<Negotiation> RequireAccessibleAsync(CallerContext caller, Guid id, CancellationToken ct)
    {
        var negotiation = await RequireAsync(id, ct);
        if (caller.IsInRole(UserRoles.Admin) || caller.IsInRole(UserRoles.Staff) || await IsOwnerAsync(caller, negotiation, ct))
        {
            return negotiation;
        }

        throw new ForbiddenAccessException();
    }

    private async Task<bool> IsOwnerAsync(CallerContext caller, Negotiation negotiation, CancellationToken ct)
    {
        var customer = await customers.GetByIdentityAsync(caller.UserId, ct);
        return customer is not null && customer.Id == negotiation.CustomerId;
    }

    private async Task<PagedResult<NegotiationResponse>> ToPagedAsync(
        IQueryable<Negotiation> q, PageQuery page, CancellationToken ct)
    {
        var total = await q.LongCountAsync(ct);
        var items = await q
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip(page.Skip).Take(page.SafePageSize)
            .ToListAsync(ct);
        return new PagedResult<NegotiationResponse>(
            items.Select(Map).ToList(), page.SafePage, page.SafePageSize, total);
    }

    private NegotiationResponse Map(Negotiation n) => new(
        n.Id.Value, n.ProductId.Value, n.BasePrice.Value, n.CurrentOffer.Value,
        n.Status.ToString(), n.ProposalsUsed, n.RemainingProposals(policy),
        n.CreatedAtUtc, n.LastProposalAtUtc, n.DecidedAtUtc);
}
```

Note: `ListMineAsync` uses `IQueryable` composition — repository `Query()` returns `IQueryable<Negotiation>`; Application referencing `Microsoft.EntityFrameworkCore` for `EF.Functions.ILike`/async extensions means Application needs the EF Core **package** (allowed — spec bans ASP.NET refs, not EF Core; `ILike` keeps filtering provider-side). Add `Microsoft.EntityFrameworkCore` PackageReference to `Application.csproj` (CPM version 10.0.8 already pinned). Update the Task 2 csproj accordingly during execution.

`src/PriceNegotiationApp.Application/Features/Auth/AuthService.cs`:
```csharp
using PriceNegotiationApp.Application.Abstractions;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Application.Exceptions;
using PriceNegotiationApp.Application.Responses;

namespace PriceNegotiationApp.Application.Features.Auth;

public sealed class AuthService(IUserAccountStore accounts, IJwtTokenGenerator jwt) : IAuthService
{
    public async Task<RegistrationResponse> RegisterAsync(string email, string password, CancellationToken ct)
    {
        var outcome = await accounts.RegisterAsync(email, password, ct);
        if (!outcome.Succeeded)
        {
            throw new ConflictException(ErrorCodes.EmailAlreadyRegistered, outcome.ErrorDescription ?? "Registration failed.");
        }

        return new RegistrationResponse(outcome.UserId);
    }

    public async Task<AuthResponse> LoginAsync(string email, string password, CancellationToken ct)
    {
        var signIn = await accounts.PasswordSignInAsync(email, password);
        switch (signIn)
        {
            case SignInResultKind.LockedOut:
                throw new UnauthorizedException(ErrorCodes.AccountLocked, "Account temporarily locked.");
            case SignInResultKind.Failure:
                throw new UnauthorizedException(ErrorCodes.InvalidCredentials, "Invalid credentials.");
        }

        // Success path: resolve identity by re-querying store
        var userId = await accounts.ResolveUserIdByEmailAsync(email, ct);
        var roles = await accounts.GetRolesAsync(userId, ct);
        var (token, expiresAtUtc) = await jwt.GenerateAsync(userId, email, roles);
        return new AuthResponse(token, expiresAtUtc, email, roles);
    }

    public CurrentUserResponse CurrentUserAsync(CallerContext caller) =>
        new(caller.UserId, caller.Email, caller.Roles.ToList());
}
```

Add to `IUserAccountStore`: `Task<Guid> ResolveUserIdByEmailAsync(string email, CancellationToken ct);` (implementation uses `UserManager.FindByEmailAsync`; throws `NotFoundException` when absent). Update the canonical-map entry accordingly.

`src/PriceNegotiationApp.Application/DependencyInjection.cs`:
```csharp
using Microsoft.Extensions.DependencyInjection;
using PriceNegotiationApp.Application.Features.Auth;
using PriceNegotiationApp.Application.Features.Negotiations;
using PriceNegotiationApp.Application.Features.Products;
using PriceNegotiationApp.Domain.Policy;

namespace PriceNegotiationApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<INegotiationPolicy, DefaultNegotiationPolicy>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<INegotiationService, NegotiationService>();
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
```

- [ ] **Step 5: Service unit tests (NSubstitute)**

`tests/PriceNegotiationApp.UnitTests/Application/NegotiationServiceShould.cs` — representative coverage (write all of these):
- `CreateAsync_throws_NotFound_for_unknown_product` (repo substitute returns null)
- `CreateAsync_throws_Conflict_when_open_negotiation_exists`
- `CreateAsync_maps_response_with_remaining_proposals`
- `CounterProposeAsync_throws_Forbidden_when_not_owner`
- `CounterProposeAsync_throws_Conflict_NoProposalsRemaining_when_budget_spent`
- `WithdrawAsync_allows_admin_for_any_negotiation`
- `GetAsync_allows_staff_but_forbids_stranger`

Substitute setup example used throughout:
```csharp
private readonly INegotiationRepository _negotiations = Substitute.For<INegotiationRepository>();
private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
private readonly NegotiationService _sut;

public NegotiationServiceShould()
{
    var policy = new DefaultNegotiationPolicy();
    _sut = new NegotiationService(_negotiations, Substitute.For<IProductRepository>(), _customers, policy,
        Substitute.For<IUnitOfWork>(), new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
}
```
Use `Microsoft.Extensions.TimeProvider.Testing` `FakeTimeProvider` — add package via `dotnet add package Microsoft.Extensions.TimeProvider.Testing` (test project only). Owner setup: `_customers.GetByIdentityAsync(userId, Arg.Any<CancellationToken>()).Returns(Customer.Create(userId))`.

`tests/PriceNegotiationApp.UnitTests/Application/ProductServiceShould.cs` — cover search filter expression building (via InMemory-less approach: use a fake IQueryable list with `AsQueryable()` substitute for `Query()`), paging math, NotFound throws, trim-on-create. Note: `ToListAsync` requires `IAsyncEnumerable` — for pure-unit testing use `Microsoft.EntityFrameworkCore.InMemory`? That contradicts dependency removal... Pragmatic call: test `ProductService.ListAsync` through the **SQLite in-memory** provider? Also heavy. Simplest honest option: keep list-filtering covered by **integration tests** (Task 11 does exactly that) and restrict `ProductServiceShould` to Get/Create/Update/Delete paths with substituted repo (no LINQ-to-entities execution needed). Do that; do NOT add InMemory back.

- [ ] **Step 6: Validate & commit**

```pwsh
dotnet build && dotnet test tests/PriceNegotiationApp.UnitTests
git add -A && git commit -m "Rebuild application layer: feature services, ports, error taxonomy, unit tests"
```

---

### Task 5: Infrastructure persistence + repositories + DI

**Files:**
- Create: `Identity/ApplicationUser.cs`, `Persistence/AppDbContext.cs`, `Persistence/DbEntityConfigurations/*.cs`, `Persistence/Repositories/*.cs`, `Data/DesignTimeDbContextFactory.cs`, `DependencyInjection.cs`
- Modify: `Directory.Packages.props` (add Npgsql, NamingConventions via `dotnet add package`)

**Interfaces:**
- Produces: `AddInfrastructure(IServiceCollection, IConfiguration)` registering DbContext/repos/UoW; design-time factory for migrations.

- [ ] **Step 1: Add packages**

```pwsh
dotnet add src/PriceNegotiationApp.Infrastructure/package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add src/PriceNegotiationApp.Infrastructure/package EFCore.NamingConventions
dotnet add src/PriceNegotiationApp.Application/package Microsoft.EntityFrameworkCore --version 10.0.8
```

- [ ] **Step 2: DbContext + configurations**

`src/PriceNegotiationApp.Infrastructure/Identity/ApplicationUser.cs`:
```csharp
using Microsoft.AspNetCore.Identity;

namespace PriceNegotiationApp.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>;
```

`src/PriceNegotiationApp.Infrastructure/Persistence/AppDbContext.cs`:
```csharp
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Domain.Models;
using PriceNegotiationApp.Infrastructure.Identity;

namespace PriceNegotiationApp.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Product> Products => Set<Product>();

    public DbSet<Negotiation> Negotiations => Set<Negotiation>();

    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("roles");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
    }
}
```
(`using Microsoft.AspNetCore.Identity;` needed for generic Identity entity types.)

`src/PriceNegotiationApp.Infrastructure/Persistence/DbEntityConfigurations/ProductConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceNegotiationApp.Domain.Models;
using PriceNegotiationApp.Domain.ValueObjects;

namespace PriceNegotiationApp.Infrastructure.Persistence.DbEntityConfigurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasConversion(new ProductIdEfCoreValueConverter()).ValueGeneratedNever();
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Price).HasConversion(new PriceEfCoreValueConverter()).HasColumnType("numeric(18,2)");
        builder.Property(p => p.Version).IsRowVersion();
    }
}
```
(`using PriceNegotiationApp.Domain.ValueObjects.Ids;` where ID converters are referenced.)

`src/PriceNegotiationApp.Infrastructure/Persistence/DbEntityConfigurations/NegotiationConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceNegotiationApp.Domain.Models;
using PriceNegotiationApp.Domain.ValueObjects;
using PriceNegotiationApp.Domain.ValueObjects.Ids;

namespace PriceNegotiationApp.Infrastructure.Persistence.DbEntityConfigurations;

public sealed class NegotiationConfiguration : IEntityTypeConfiguration<Negotiation>
{
    public void Configure(EntityTypeBuilder<Negotiation> builder)
    {
        builder.ToTable("negotiations");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasConversion(new NegotiationIdEfCoreValueConverter()).ValueGeneratedNever();
        builder.Property(n => n.ProductId).HasConversion(new ProductIdEfCoreValueConverter());
        builder.Property(n => n.CustomerId).HasConversion(new CustomerIdEfCoreValueConverter());
        builder.Property(n => n.BasePrice).HasConversion(new PriceEfCoreValueConverter()).HasColumnType("numeric(18,2)");
        builder.Property(n => n.CurrentOffer).HasConversion(new PriceEfCoreValueConverter()).HasColumnType("numeric(18,2)");
        builder.Property(n => n.Status).HasConversion<int>();
        builder.HasOne<Product>().WithMany().HasForeignKey(n => n.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(n => n.CustomerId).OnDelete(DeleteBehavior.Cascade);
        // One OPEN negotiation per customer per product; closed history preserved.
        builder.HasIndex(n => new { n.ProductId, n.CustomerId })
               .IsUnique()
               .HasFilter($"status = {(int)NegotiationStatus.Open}");
        builder.Property(n => n.Version).IsRowVersion();
    }
}
```

`src/PriceNegotiationApp.Infrastructure/Persistence/DbEntityConfigurations/CustomerConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceNegotiationApp.Domain.Models;
using PriceNegotiationApp.Domain.ValueObjects.Ids;

namespace PriceNegotiationApp.Infrastructure.Persistence.DbEntityConfigurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasConversion(new CustomerIdEfCoreValueConverter()).ValueGeneratedNever();
        builder.HasIndex(c => c.IdentityUserId).IsUnique();
    }
}
```

- [ ] **Step 3: Repositories + UoW**

`src/PriceNegotiationApp.Infrastructure/Persistence/Repositories/ProductRepository.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Application.Abstractions;
using PriceNegotiationApp.Domain.Models;
using PriceNegotiationApp.Domain.ValueObjects.Ids;

namespace PriceNegotiationApp.Infrastructure.Persistence.Repositories;

public sealed class ProductRepository(AppDbContext db) : IProductRepository
{
    public Task<Product?> GetAsync(ProductId id, CancellationToken ct) =>
        db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);

    public IQueryable<Product> Query() => db.Products.AsNoTracking();

    public async Task AddAsync(Product product, CancellationToken ct)
    {
        await db.Products.AddAsync(product, ct);
    }

    public void Remove(Product product) => db.Products.Remove(product);
}
```

`src/PriceNegotiationApp.Infrastructure/Persistence/Repositories/NegotiationRepository.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Application.Abstractions;
using PriceNegotiationApp.Domain.Models;
using PriceNegotiationApp.Domain.ValueObjects.Ids;

namespace PriceNegotiationApp.Infrastructure.Persistence.Repositories;

public sealed class NegotiationRepository(AppDbContext db, ICustomerRepository customers) : INegotiationRepository
{
    public Task<Negotiation?> GetAsync(NegotiationId id, CancellationToken ct) =>
        db.Negotiations.FirstOrDefaultAsync(n => n.Id == id, ct);

    public IQueryable<Negotiation> Query() => db.Negotiations.AsNoTracking();

    public async Task AddAsync(Negotiation negotiation, CancellationToken ct) =>
        await db.Negotiations.AddAsync(negotiation, ct);

    public async Task<Negotiation?> FindOpenAsync(ProductId productId, Guid identityUserId, CancellationToken ct)
    {
        var customer = await customers.GetByIdentityAsync(identityUserId, ct);
        if (customer is null)
        {
            return null;
        }

        return await db.Negotiations.FirstOrDefaultAsync(
            n => n.ProductId == productId && n.CustomerId == customer.Id && n.Status == NegotiationStatus.Open, ct);
    }

    public void Remove(Negotiation negotiation) => db.Negotiations.Remove(negotiation);
}
```

`src/PriceNegotiationApp.Infrastructure/Persistence/Repositories/CustomerRepository.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Application.Abstractions;
using PriceNegotiationApp.Domain.Models;
using PriceNegotiationApp.Domain.ValueObjects.Ids;

namespace PriceNegotiationApp.Infrastructure.Persistence.Repositories;

public sealed class CustomerRepository(AppDbContext db, IUnitOfWork uow) : ICustomerRepository
{
    public async Task<CustomerId> GetOrCreateAsync(Guid identityUserId, CancellationToken ct)
    {
        var existing = await GetByIdentityAsync(identityUserId, ct);
        if (existing is not null)
        {
            return existing.Id;
        }

        var customer = Customer.Create(identityUserId);
        await db.Customers.AddAsync(customer, ct);
        await uow.SaveChangesAsync(ct);
        return customer.Id;
    }

    public Task<Customer?> GetByIdentityAsync(Guid identityUserId, CancellationToken ct) =>
        db.Customers.FirstOrDefaultAsync(c => c.IdentityUserId == identityUserId, ct);
}
```
(Circular DI between `NegotiationRepository(ICustomerRepository)` and `CustomerRepository(IUnitOfWork)` is fine — no cycle.)

`src/PriceNegotiationApp.Infrastructure/Persistence/Repositories/UnitOfWork.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Application.Abstractions;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Application.Exceptions;

namespace PriceNegotiationApp.Infrastructure.Persistence.Repositories;

public sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            return await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(ErrorCodes.ConcurrencyConflict, "The resource was modified concurrently. Reload and retry.");
        }
    }
}
```

- [ ] **Step 4: Design-time factory + DI registration**

`src/PriceNegotiationApp.Infrastructure/Data/DesignTimeDbContextFactory.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PriceNegotiationApp.Infrastructure.Persistence;

namespace PriceNegotiationApp.Infrastructure.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("Database__ConnectionString")
                               ?? "Host=localhost;Port=5432;Database=pricenego_design;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options);
    }
}
```

`src/PriceNegotiationApp.Infrastructure/DependencyInjection.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PriceNegotiationApp.Application.Abstractions;
using PriceNegotiationApp.Infrastructure.Identity;
using PriceNegotiationApp.Infrastructure.Persistence;
using PriceNegotiationApp.Infrastructure.Persistence.Repositories;

namespace PriceNegotiationApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration["Database:ConnectionString"])
                   .UseSnakeCaseNamingConvention());

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<INegotiationRepository, NegotiationRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();

        return services;
    }
}
```
(Identity/JWT/seeding registrations land in Task 6 inside the same method — extend, don't replace.)

- [ ] **Step 5: Validate & commit**

```pwsh
dotnet build
git add -A && git commit -m "Add PostgreSQL persistence: DbContext, snake_case configs, partial open-negotiation index, xmin versions, repositories"
```

---

### Task 6: JWT, Identity account store, seeding, migrations

**Files:**
- Create: `Auth/{JwtOptions,JwtOptionsValidator,JwtManager}.cs`, `Identity/IdentityAccountStore.cs`, `Seeding/{SeedingOptions,SeedingHostedService}.cs`
- Modify: `DependencyInjection.cs` (extend), run initial migration
- Test: `tests/PriceNegotiationApp.UnitTests/Infrastructure/JwtManagerShould.cs`

**Interfaces:**
- Consumes: `IUserAccountStore`, `IJwtTokenGenerator` ports from Task 4.
- Produces: working register/login mechanics; database schema via migration; startup seeding.

- [ ] **Step 1: JwtOptions + validator + manager**

`src/PriceNegotiationApp.Infrastructure/Auth/JwtOptions.cs`:
```csharp
namespace PriceNegotiationApp.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string Issuer { get; init; }

    public required string Audience { get; init; }

    public required string SecretKey { get; init; }

    public int ExpiryMinutes { get; init; } = 60;
}
```

`src/PriceNegotiationApp.Infrastructure/Auth/JwtOptionsValidator.cs`:
```csharp
using Microsoft.Extensions.Options;

namespace PriceNegotiationApp.Infrastructure.Auth;

public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var failures = new List<string>();
        if (options.SecretKey.Length < 32)
        {
            failures.Add("Jwt:SecretKey must be at least 32 characters.");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("Jwt:Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Jwt:Audience is required.");
        }

        if (options.ExpiryMinutes < 1)
        {
            failures.Add("Jwt:ExpiryMinutes must be >= 1.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
```

`src/PriceNegotiationApp.Infrastructure/Auth/JwtManager.cs`:
```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PriceNegotiationApp.Application.Abstractions;

namespace PriceNegotiationApp.Infrastructure.Auth;

public sealed class JwtManager(IOptions<JwtOptions> options, TimeProvider clock) : IJwtTokenGenerator
{
    public Task<(string Token, DateTimeOffset ExpiresAtUtc)> GenerateAsync(Guid userId, string email, IReadOnlyCollection<string> roles)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow();
        var expiresAtUtc = now.AddMinutes(settings.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SecretKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: credentials);

        return Task.FromResult((new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc));
    }
}
```

- [ ] **Step 2: Identity account store**

`src/PriceNegotiationApp.Infrastructure/Identity/IdentityAccountStore.cs`:
```csharp
using Microsoft.AspNetCore.Identity;
using PriceNegotiationApp.Application.Abstractions;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Application.Exceptions;

namespace PriceNegotiationApp.Infrastructure.Identity;

public sealed class IdentityAccountStore(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    : IUserAccountStore
{
    private static readonly RegistrationOutcome DuplicateEmail = new(false, Guid.Empty, "Email already registered.");

    public async Task<RegistrationOutcome> RegisterAsync(string email, string password, CancellationToken ct)
    {
        var user = new ApplicationUser { UserName = email, Email = email };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return result.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName")
                ? DuplicateEmail
                : ValidationFailed(result.Errors);
        }

        await userManager.AddToRoleAsync(user, UserRoles.Customer);
        return new RegistrationOutcome(true, user.Id, null);
    }

    public async Task<SignInResultKind> PasswordSignInAsync(string email, string password)
    {
        var result = await signInManager.PasswordSignInAsync(email, password, isPersistent: false, lockoutOnFailure: true);
        return result.Succeeded ? SignInResultKind.Success
             : result.IsLockedOut ? SignInResultKind.LockedOut
             : SignInResultKind.Failure;
    }

    public async Task<Guid> ResolveUserIdByEmailAsync(string email, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(email)
                   ?? throw new NotFoundException("User", email);
        return user.Id;
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
                   ?? throw new NotFoundException("User", userId);
        return await userManager.GetRolesAsync(user);
    }

    private static RegistrationOutcome ValidationFailed(IEnumerable<IdentityError> errors) =>
        new(false, Guid.Empty, string.Join("; ", errors.Select(e => e.Description)));
}
```

- [ ] **Step 3: Seeding**

`src/PriceNegotiationApp.Infrastructure/Seeding/SeedingOptions.cs`:
```csharp
namespace PriceNegotiationApp.Infrastructure.Seeding;

public sealed class SeedingOptions
{
    public const string SectionName = "Seeding";

    public string AdminEmail { get; init; } = "admin@app.com";

    public string AdminPassword { get; init; } = string.Empty;

    public string StaffEmail { get; init; } = "staff@app.com";

    public string StaffPassword { get; init; } = string.Empty;

    public bool SeedSampleProducts { get; init; }
}
```

`src/PriceNegotiationApp.Infrastructure/Seeding/SeedingHostedService.cs`:
```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Domain.Models;
using PriceNegotiationApp.Domain.ValueObjects;
using PriceNegotiationApp.Infrastructure.Identity;
using PriceNegotiationApp.Infrastructure.Persistence;

namespace PriceNegotiationApp.Infrastructure.Seeding;

public sealed class SeedingHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<SeedingOptions> seedingOptions,
    ILogger<SeedingHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(cancellationToken);

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { UserRoles.Admin, UserRoles.Staff, UserRoles.Customer })
        {
            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await EnsureUserAsync(userManager, seedingOptions.Value.AdminEmail, seedingOptions.Value.AdminPassword, UserRoles.Admin, cancellationToken);
        await EnsureUserAsync(userManager, seedingOptions.Value.StaffEmail, seedingOptions.Value.StaffPassword, UserRoles.Staff, cancellationToken);

        if (seedingOptions.Value.SeedSampleProducts && !await db.Products.AnyAsync(cancellationToken))
        {
            db.Products.AddRange(
                Product.Create("Mechanical Keyboard", Price.From(249.00m)),
                Product.Create("Wireless Mouse", Price.From(79.90m)),
                Product.Create("USB-C Docking Station", Price.From(189.50m)));
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Database migrated and seed data ensured.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager, string email, string password, string role, CancellationToken ct)
    {
        if (await userManager.FindByEmailAsync(email) is not null || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var user = new ApplicationUser { UserName = email, Email = email };
        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }
}
```

- [ ] **Step 4: Extend `AddInfrastructure`** — append before `return services;`

```csharp
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<ISignInManagerAdapter?>(...) // NOT NEEDED — see below
```
Final correct block (replace the sketch above):
```csharp
        services.AddScoped<IUserAccountStore, IdentityAccountStore>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services.AddSingleton<IJwtTokenGenerator, JwtManager>();

        services.AddOptions<SeedingOptions>()
            .Bind(configuration.GetSection(SeedingOptions.SectionName));

        services.AddHostedService<SeedingHostedService>();
```

- [ ] **Step 5: Initial migration**

```pwsh
dotnet tool install --global dotnet-ef
$env:Database__ConnectionString = 'Host=localhost;Port=5432;Database=pricenego_design;Username=postgres;Password=postgres'
dotnet ef migrations add Initial --project src/PriceNegotiationApp.Infrastructure --startup-project src/PriceNegotiationApp.Infrastructure --output-dir Data/Migrations
```
Review generated migration for: snake_case table/column names, partial index filter `status = 1`, numeric(18,2) columns, identity tables renamed (`users`, `roles`, ...). Commit the generated files.

- [ ] **Step 6: JwtManager unit test**

`tests/PriceNegotiationApp.UnitTests/Infrastructure/JwtManagerShould.cs`:
```csharp
using Microsoft.Extensions.Options;
using PriceNegotiationApp.Infrastructure.Auth;

namespace PriceNegotiationApp.UnitTests.Infrastructure;

public class JwtManagerShould
{
    [Fact]
    public async Task Generate_token_with_sub_email_role_and_expiry()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SecretKey = new string('k', 48),
            ExpiryMinutes = 30,
        });
        var clock = new FixedTimeProvider();
        var sut = new JwtManager(options, clock);

        var (token, expiresAtUtc) = await sut.GenerateAsync(
            Guid.NewGuid(), "user@test.dev", ["Customer"]);

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.True(token.Split('.').Length == 3);
        var expected = clock.GetUtcNow().AddMinutes(30);
        Assert.True((expiresAtUtc - expected).Duration() < TimeSpan.FromSeconds(1));
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }
}
```

- [ ] **Step 7: Validate & commit**

```pwsh
dotnet build && dotnet test tests/PriceNegotiationApp.UnitTests
git add -A && git commit -m "Add hardened JWT issuance, Identity-backed account store, config-driven seeding, initial PG migration"
```

---

### Task 7: Api host wiring

**Files:**
- Create: `Extensions/WebApplicationBuilderExtensions.cs`, `Extensions/PipelineExtensions.cs`, `Extensions/ClaimsPrincipalExtensions.cs`, `Extensions/EndpointConventionExtensions.cs`, `GlobalExceptionHandler.cs`
- Rewrite: `src/PriceNegotiationApp.Api/Program.cs`, `appsettings.json`
- Delete: `appsettings.Development.json` secrets content (rewrite without secrets)

**Interfaces:**
- Produces: `AddApiServices()`, `UsePipeline()`, `ToCallerContext()`, `RequireRoles<T>()` — used by modules in Tasks 8–9.

- [ ] **Step 1: appsettings.json (structural defaults only, NO secrets)**

```json
{
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "AllowedHosts": "*",
  "Cors": { "AllowedOrigins": [] },
  "Jwt": { "Issuer": "", "Audience": "", "SecretKey": "", "ExpiryMinutes": 60 },
  "Database": { "ConnectionString": "" },
  "Seeding": { "SeedSampleProducts": false }
}
```
Delete `src/PriceNegotiationApp.Api/appsettings.Development.json` entirely; local overrides go to user-secrets:
```pwsh
dotnet user-secrets set "Jwt:SecretKey" "dev-only-secret-key-change-me-32-chars-min!!" --project src/PriceNegotiationApp.Api
dotnet user-secrets set "Jwt:Issuer" "https://localhost:5185" --project src/PriceNegotiationApp.Api
dotnet user-secrets set "Jwt:Audience" "price-negotiation-api" --project src/PriceNegotiationApp.Api
dotnet user-secrets set "Database:ConnectionString" "Host=localhost;Port=5432;Database=pricenego_dev;Username=postgres;Password=postgres" --project src/PriceNegotiationApp.Api
dotnet user-secrets set "Seeding:AdminPassword" "Admin123!" --project src/PriceNegotiationApp.Api
dotnet user-secrets set "Seeding:StaffPassword" "Staff123!" --project src/PriceNegotiationApp.Api
```

- [ ] **Step 2: GlobalExceptionHandler**

`src/PriceNegotiationApp.Api/GlobalExceptionHandler.cs`:
```csharp
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Application.Exceptions;
using PriceNegotiationApp.Domain.Exceptions;

namespace PriceNegotiationApp.Api;

public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetailsService, IHostEnvironment environment)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, code) = exception switch
        {
            ProposalExceedsLimitException => (StatusCodes.Status400BadRequest, "Proposal rejected", ErrorCodes.ProposalExceedsLimit),
            DomainException => (StatusCodes.Status409Conflict, "Business rule violated", ErrorCodes.NegotiationClosed),
            NotFoundException notFound => (StatusCodes.Status404NotFound, "Resource not found", notFound.Code),
            ConflictException conflict => (StatusCodes.Status409Conflict, "Conflict", conflict.Code),
            ForbiddenAccessException => (StatusCodes.Status403Forbidden, "Forbidden", ErrorCodes.Forbidden),
            UnauthorizedException unauthorized => (StatusCodes.Status401Unauthorized, "Authentication failed", unauthorized.Code),
            OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested
                => (499, "Request cancelled", "client_closed_request"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error", ErrorCodes.InternalError),
        };

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = environment.IsDevelopment() && exception is not OperationCanceledException ? exception.Message : null,
                Extensions = { ["code"] = code },
            },
        });
    }
}
```

- [ ] **Step 3: Extension plumbing**

`src/PriceNegotiationApp.Api/Extensions/ClaimsPrincipalExtensions.cs`:
```csharp
using System.Security.Claims;
using PriceNegotiationApp.Application.Common;

namespace PriceNegotiationApp.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static CallerContext ToCallerContext(this ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return CallerContext.Anonymous;
        }

        _ = Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId);
        var email = principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet();
        return new CallerContext(userId, email, roles);
    }
}
```

`src/PriceNegotiationApp.Api/Extensions/EndpointConventionExtensions.cs`:
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;

namespace PriceNegotiationApp.Api.Extensions;

public static class EndpointConventionExtensions
{
    public static TBuilder RequireRoles<TBuilder>(this TBuilder builder, params string[] roles)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(new AuthorizeAttribute { Roles = string.Join(",", roles) });
}
```

`src/PriceNegotiationApp.Api/Extensions/WebApplicationBuilderExtensions.cs`:
```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using System.Text;

namespace PriceNegotiationApp.Api.Extensions;

public static class WebApplicationBuilderExtensions
{
    public const string AuthRateLimitPolicy = "auth";
    public const string CorsPolicy = "api";
    public const string ShortCachePolicy = "short";

    public static WebApplicationBuilder AddApiServices(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;

        builder.Host.UseSerilog((context, _, logConfiguration) => logConfiguration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine("logs", "api-.log"), rollingInterval: RollingInterval.Day));

        builder.Services
            .AddApplicationServices()
            .AddInfrastructure(configuration);

        builder.Services.AddProblemDetails(options =>
                options.CustomizeProblemDetails = context =>
                    context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier))
            .AddExceptionHandler<GlobalExceptionHandler>();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var jwt = configuration.GetSection("Jwt").Get<JwtSettings>()!;
                options.MapInboundClaims = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                };
            });
        builder.Services.AddAuthorization();

        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length > 0)
        {
            builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
                policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
        }

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter(AuthRateLimitPolicy, windowOptions =>
            {
                windowOptions.PermitLimit = 10;
                windowOptions.Window = TimeSpan.FromMinutes(1);
                windowOptions.QueueLimit = 0;
            });
        });

        builder.Services.AddOutputCache(options => options.AddPolicy(ShortCachePolicy,
            policy => policy.Expire(TimeSpan.FromSeconds(30))
                .SetVaryByQuery("search", "minPrice", "maxPrice", "sortBy", "sortDesc", "page", "pageSize")));

        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
            .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

        builder.Services.AddOpenApi();

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("PriceNegotiationApp.Api"))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation())
            .UseOtlpExporter();

        return builder;
    }
```
Notes for the implementer:
- `JwtSettings` here is a small local bind record in the Api (`Extensions/JwtSettings.cs`): `public sealed class JwtSettings { public required string Issuer {get;init;} public required string Audience {get;init;} public required string SecretKey {get;init;} }` — Infrastructure's validated `JwtOptions` remains the enforcement point; Api binds its own view for bearer params.
- Required usings include `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.EntityFrameworkCore` (for `AddDbContextCheck` extension from the health-checks EF package), `Microsoft.IdentityModel.Tokens`, `System.Text`, `PriceNegotiationApp.Infrastructure.Persistence`.
- Before first successful run you must install missing packages into the Api project: `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Http`, `OpenTelemetry.Exporter.OpenTelemetryProtocol` (use `dotnet add package`; CPM updates automatically).
- `UseOtlpExporter()` is a no-op unless `OTEL_EXPORTER_OTLP_ENDPOINT` is set.

`src/PriceNegotiationApp.Api/Extensions/PipelineExtensions.cs`:
```csharp
using Serilog;

namespace PriceNegotiationApp.Api.Extensions;

public static class PipelineExtensions
{
    public static WebApplication UsePipeline(this WebApplication app)
    {
        app.UseSerilogRequestLogging();
        app.UseStatusCodePages();

        app.UseExceptionHandler();
        app.UseHttpsRedirection();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.UseOutputCache();

        // Task 7 ships only health endpoints; module mappings are added in Tasks 8-9:
        app.MapHealthChecks("/health/live", new() { Predicate = r => r.Tags.Contains("live") });
        app.MapHealthChecks("/health/ready", new() { Predicate = r => r.Tags.Contains("ready") });

        return app;
    }
}
```
HSTS is enabled before `UseHttpsRedirection` for non-development environments: add `if (!app.Environment.IsDevelopment()) app.UseHsts();` immediately above `app.UseHttpsRedirection();`.
In Task 8 a `MapModules(this WebApplication)` private extension replaces the two direct `MapHealthChecks` lines (health checks move inside it).

- [ ] **Step 4: Final Program.cs**

```csharp
using PriceNegotiationApp.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.AddApiServices();

var app = builder.Build();
app.UsePipeline();

app.Run();

public partial class Program;
```

- [ ] **Step 5: Validate & commit**

```pwsh
dotnet build
# Smoke-run against a local postgres (or skip run; full validation comes with integration tests):
dotnet run --project src/PriceNegotiationApp.Api   # expect: migration + seeding logs, GET /health/live = Healthy
git add -A && git commit -m "Wire API host: strict JWT validation, ProblemDetails handler, rate limiting, CORS, output cache, health checks, OTel"
```

---

### Task 8: Auth + Products modules

**Files:**
- Create: `Contracts/AuthRequests.cs`, `Contracts/ProductRequests.cs`, `Modules/AuthModule.cs`, `Modules/ProductsModule.cs`
- Modify: `PipelineExtensions` (call `MapModules`), `Program.cs` unchanged

**Interfaces:**
- Consumes: `IAuthService`, `IProductService`, `CallerContext.ToCallerContext()`, `RequireRoles`.
- Produces: routes per spec §6 (auth + products); `MapModules` aggregate.

- [ ] **Step 1: Request contracts**

`src/PriceNegotiationApp.Api/Contracts/AuthRequests.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace PriceNegotiationApp.Api.Contracts;

public sealed class RegisterRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; init; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}
```
(.NET 10 built-in validation (`AddValidation()`) validates these automatically; add `builder.Services.AddValidation();` in `AddApiServices`.)

`src/PriceNegotiationApp.Api/Contracts/ProductRequests.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace PriceNegotiationApp.Api.Contracts;

public sealed class CreateProductRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [Required, Range(0.01, 999_999_999)]
    public decimal Price { get; init; }
}

public sealed class UpdateProductRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [Required, Range(0.01, 999_999_999)]
    public decimal Price { get; init; }
}
```

- [ ] **Step 2: AuthModule**

`src/PriceNegotiationApp.Api/Modules/AuthModule.cs`:
```csharp
using Microsoft.AspNetCore.Mvc;
using PriceNegotiationApp.Api.Contracts;
using PriceNegotiationApp.Api.Extensions;
using PriceNegotiationApp.Application.Features.Auth;

namespace PriceNegotiationApp.Api.Modules;

public static class AuthModule
{
    public static IEndpointRouteBuilder MapAuthApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/register",
                async (RegisterRequest request, IAuthService auth, CancellationToken ct) =>
                    TypedResults.Created($"/api/v1/auth/me", await auth.RegisterAsync(request.Email, request.Password, ct)))
            .RequireRateLimiting(WebApplicationBuilderExtensions.AuthRateLimitPolicy)
            .AllowAnonymous();

        group.MapPost("/login",
                async (LoginRequest request, IAuthService auth, CancellationToken ct) =>
                    TypedResults.Ok(await auth.LoginAsync(request.Email, request.Password, ct)))
            .RequireRateLimiting(WebApplicationBuilderExtensions.AuthRateLimitPolicy)
            .AllowAnonymous();

        group.MapGet("/me",
                (ClaimsPrincipal principal, IAuthService auth) =>
                    TypedResults.Ok(auth.CurrentUserAsync(principal.ToCallerContext())))
            .RequireAuthorization();

        return app;
    }
}
```

- [ ] **Step 3: ProductsModule**

`src/PriceNegotiationApp.Api/Modules/ProductsModule.cs`:
```csharp
using PriceNegotiationApp.Api.Contracts;
using PriceNegotiationApp.Api.Extensions;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Application.Features.Products;

namespace PriceNegotiationApp.Api.Modules;

public static class ProductsModule
{
    public static IEndpointRouteBuilder MapProductsApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/products").WithTags("Products");

        group.MapGet("/",
                async ([AsParameters] ProductListRequest query, IProductService products, CancellationToken ct) =>
                    TypedResults.Ok(await products.ListAsync(query.ToQuery(), ct)))
            .CacheOutput(WebApplicationBuilderExtensions.ShortCachePolicy)
            .AllowAnonymous();

        group.MapGet("/{id:guid}",
                async (Guid id, IProductService products, CancellationToken ct) =>
                    TypedResults.Ok(await products.GetAsync(id, ct)))
            .WithName("GetProductById")
            .CacheOutput(WebApplicationBuilderExtensions.ShortCachePolicy)
            .AllowAnonymous();

        group.MapPost("/", async (CreateProductRequest request, IProductService products, CancellationToken ct) =>
        {
            var created = await products.CreateAsync(request.Name, request.Price, ct);
            return TypedResults.CreatedAtRoute(created, "GetProductById", new { id = created.Id });
        }).RequireRoles(UserRoles.Admin, UserRoles.Staff);

        group.MapPut("/{id:guid}", async (Guid id, UpdateProductRequest request, IProductService products, CancellationToken ct) =>
                TypedResults.Ok(await products.UpdateAsync(id, request.Name, request.Price, ct)))
            .RequireRoles(UserRoles.Admin, UserRoles.Staff);

        group.MapDelete("/{id:guid}", async (Guid id, IProductService products, CancellationToken ct) =>
        {
            await products.DeleteAsync(id, ct);
            return TypedResults.NoContent();
        }).RequireRoles(UserRoles.Admin);

        return app;
    }
}
```
with `using PriceNegotiationApp.Application.Common;` for `UserRoles` and a small mapper on the request side:
`src/PriceNegotiationApp.Api/Contracts/ProductRequests.cs` append:
```csharp
public sealed class ProductListRequest
{
    public string? Search { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public string? SortBy { get; init; }
    public bool SortDesc { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;

    public ProductQuery ToQuery() => new(Search, MinPrice, MaxPrice, SortBy, SortDesc, Page, PageSize);
}
```

- [ ] **Step 4: Aggregate + wire**

`src/PriceNegotiationApp.Api/Extensions/PipelineExtensions.cs` — add:
```csharp
    private static void MapModules(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new() { Predicate = r => r.Tags.Contains("live") });
        app.MapHealthChecks("/health/ready", new() { Predicate = r => r.Tags.Contains("ready") });
        app.MapAuthApi();
        app.MapProductsApi();
        // app.MapNegotiationsApi(); // added in Task 9
    }
```
and call `app.MapModules();` in `UsePipeline`, replacing the two direct `MapHealthChecks` lines.

- [ ] **Step 5: Validate & commit**

```pwsh
dotnet build && dotnet run --project src/PriceNegotiationApp.Api
# Manual smoke: POST /api/v1/auth/register, login, GET /api/v1/products?page=1
git add -A && git commit -m "Add auth and products minimal-API modules with built-in validation and output caching"
```

---

### Task 9: Negotiations module + local run polish

**Files:**
- Create: `Contracts/NegotiationRequests.cs`, `Modules/NegotiationsModule.cs`
- Modify: `PipelineExtensions.MapModules` (add negotiations), `Properties/launchSettings.json`, `PriceNegotiationApp.http`

**Interfaces:**
- Consumes: `INegotiationService` signatures from Task 4.
- Produces: complete API surface — app is feature-complete after this task.

- [ ] **Step 1: Contracts**

`src/PriceNegotiationApp.Api/Contracts/NegotiationRequests.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace PriceNegotiationApp.Api.Contracts;

public sealed class CreateNegotiationRequest
{
    [Required]
    public Guid ProductId { get; init; }

    [Required, Range(0.01, 999_999_999)]
    public decimal ProposedPrice { get; init; }
}

public sealed class CounterProposalRequest
{
    [Required, Range(0.01, 999_999_999)]
    public decimal ProposedPrice { get; init; }
}
```

- [ ] **Step 2: Module**

`src/PriceNegotiationApp.Api/Modules/NegotiationsModule.cs`:
```csharp
using PriceNegotiationApp.Api.Contracts;
using PriceNegotiationApp.Api.Extensions;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Application.Features.Negotiations;
using Microsoft.AspNetCore.Mvc;

namespace PriceNegotiationApp.Api.Modules;

public static class NegotiationsModule
{
    public static IEndpointRouteBuilder MapNegotiationsApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/negotiations").WithTags("Negotiations");

        group.MapPost("/", async (CreateNegotiationRequest request, ClaimsPrincipal principal, INegotiationService negotiations, CancellationToken ct) =>
                TypedResults.Created($"/api/v1/negotiations/mine",
                    await negotiations.CreateAsync(principal.ToCallerContext(), request.ProductId, request.ProposedPrice, ct)))
            .RequireRoles(UserRoles.Customer);

        group.MapGet("/mine", async (ClaimsPrincipal principal, INegotiationService negotiations,
                [FromQuery] int page, [FromQuery] int pageSize, CancellationToken ct) =>
                TypedResults.Ok(await negotiations.ListMineAsync(principal.ToCallerContext(), new PageQuery(page, pageSize), ct)))
            .RequireRoles(UserRoles.Customer);

        group.MapGet("/", async (INegotiationService negotiations,
                [FromQuery] int page, [FromQuery] int pageSize, CancellationToken ct) =>
                TypedResults.Ok(await negotiations.ListAsync(new PageQuery(page, pageSize), ct)))
            .RequireRoles(UserRoles.Admin, UserRoles.Staff);

        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal principal, INegotiationService negotiations, CancellationToken ct) =>
                TypedResults.Ok(await negotiations.GetAsync(principal.ToCallerContext(), id, ct)))
            .RequireAuthorization();

        group.MapPatch("/{id:guid}/proposals",
                async (Guid id, CounterProposalRequest request, ClaimsPrincipal principal, INegotiationService negotiations, CancellationToken ct) =>
                    TypedResults.Ok(await negotiations.CounterProposeAsync(principal.ToCallerContext(), id, request.ProposedPrice, ct)))
            .RequireAuthorization();

        group.MapPost("/{id:guid}/accept", async (Guid id, INegotiationService negotiations, CancellationToken ct) =>
                TypedResults.Ok(await negotiations.AcceptAsync(id, ct)))
            .RequireRoles(UserRoles.Admin, UserRoles.Staff);

        group.MapPost("/{id:guid}/decline", async (Guid id, INegotiationService negotiations, CancellationToken ct) =>
                TypedResults.Ok(await negotiations.DeclineAsync(id, ct)))
            .RequireRoles(UserRoles.Admin, UserRoles.Staff);

        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal principal, INegotiationService negotiations, CancellationToken ct) =>
            {
                await negotiations.WithdrawAsync(principal.ToCallerContext(), id, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization();

        return app;
    }
}
```

Then add `app.MapNegotiationsApi();` inside `MapModules`.

- [ ] **Step 3: launchSettings + .http rewrite**

`src/PriceNegotiationApp.Api/Properties/launchSettings.json`:
```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "launchBrowser": false,
      "applicationUrl": "http://localhost:5185",
      "environmentVariables": { "ASPNETCORE_ENVIRONMENT": "Development" }
    },
    "https": {
      "commandName": "Project",
      "launchBrowser": false,
      "applicationUrl": "https://localhost:7004;http://localhost:5185",
      "environmentVariables": { "ASPNETCORE_ENVIRONMENT": "Development" }
    }
  }
}
```

Rewrite `PriceNegotiationApp.http` with real requests (register/login/products CRUD/negotiation flow) — variables `@host = http://localhost:5185`, `@token = <paste>`; include accept/decline/counter-proposal examples.

- [ ] **Step 4: Validate & commit**

```pwsh
dotnet build && dotnet format --verify-no-changes || dotnet format
git add -A && git commit -m "Complete API surface: negotiations lifecycle endpoints, cleaned launch settings and .http scratch"
```

---

### Task 10: Integration test infrastructure + auth flow tests

**Files:**
- Create: `tests/PriceNegotiationApp.IntegrationTests/Support/{IntegrationTestFactory,PostgresFixture,BearerTokenHandler,TestUsers}.cs`
- Test: `tests/PriceNegotiationApp.IntegrationTests/AuthFlowShould.cs`

**Interfaces:**
- Produces: `[Collection("api")]` fixture giving `IntegrationTestFixture` with `Client` (anon HttpClient), `CreateUserAsync(email,password,role-expectations)` returning an authenticated client, and Refit-typed `IProductsApiClient`/`INegotiationsApiClient`/`IAuthApiClient` factories.

- [ ] **Step 1: Packages**

```pwsh
dotnet add tests/PriceNegotiationApp.IntegrationTests/package Testcontainers.PostgreSql
dotnet add tests/PriceNegotiationApp.IntegrationTests/package Refit.HttpClientFactory   # if Refit typed-client factory desired; plain Refit suffices otherwise
```

- [ ] **Step 2: Factory + fixtures**

`tests/PriceNegotiationApp.IntegrationTests/Support/PostgresFixture.cs`:
```csharp
using Testcontainers.PostgreSql;

namespace PriceNegotiationApp.IntegrationTests.Support;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    public Task InitializeAsync() => Container.StartAsync();

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "api";
}
```

`tests/PriceNegotiationApp.IntegrationTests/Support/IntegrationTestFactory.cs`:
```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using PriceNegotiationApp.IntegrationTests.Support;

namespace PriceNegotiationApp.IntegrationTests;

public sealed class IntegrationTestFactory(PostgreSqlFixture postgres) : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@test.local";
    public const string StaffEmail = "staff@test.local";
    public const string SeedPassword = "Seed123!a";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:ConnectionString", postgres.Container.GetConnectionString());
        builder.UseSetting("Jwt:Issuer", "integration-tests");
        builder.UseSetting("Jwt:Audience", "integration-tests");
        builder.UseSetting("Jwt:SecretKey", new string('t', 64));
        builder.UseSetting("Jwt:ExpiryMinutes", "30");
        builder.UseSetting("Seeding:AdminEmail", AdminEmail);
        builder.UseSetting("Seeding:AdminPassword", SeedPassword);
        builder.UseSetting("Seeding:StaffEmail", StaffEmail);
        builder.UseSetting("Seeding:StaffPassword", SeedPassword);
        builder.UseSetting("Seeding:SeedSampleProducts", "true");
    }
}
```

`tests/PriceNegotiationApp.IntegrationTests/Support/BearerTokenHandler.cs`:
```csharp
namespace PriceNegotiationApp.IntegrationTests.Support;

public sealed class TokenHolder
{
    public string? Token { get; set; }
}

public sealed class BearerTokenHandler(TokenHolder holder) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (holder.Token is { } token)
        {
            request.Headers.Authorization = new("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
```

`tests/PriceNegotiationApp.IntegrationTests/Support/TestFixture.cs`:
```csharp
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using PriceNegotiationApp.Api.Contracts;

namespace PriceNegotiationApp.IntegrationTests.Support;

public sealed class IntegrationTestFixture(PostgreSqlFixture postgres) : IAsyncLifetime
{
    public IntegrationTestFactory Factory { get; private set; } = null!;

    public HttpClient Anonymous { get; private set; } = null!;

    public Task InitializeAsync()
    {
        Factory = new IntegrationTestFactory(postgres);
        Anonymous = Factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        Anonymous.Dispose();
        await Factory.DisposeAsync();
    }

    /// <summary>Registers (idempotent-enough: unique suffix) and logs in a fresh user; returns an authorized client.</summary>
    public async Task<UserSession> CreateUserAsync(string roleHint = "customer")
    {
        var email = $"{roleHint}.{Guid.NewGuid():N}@test.local";
        var password = "Passw0rd!";
        var register = await Anonymous.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest { Email = email, Password = password });
        register.EnsureSuccessStatusCode();

        var login = await Anonymous.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest { Email = email, Password = password });
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return new UserSession(this, email, auth!.AccessToken);
    }

    public HttpClient ClientFor(string? token)
    {
        var holder = new TokenHolder { Token = token };
        return Factory.CreateDefaultClient(new BearerTokenHandler(holder));
    }
}

public sealed class UserSession(IntegrationTestFixture fixture, string email, string token)
{
    public string Email { get; } = email;

    public HttpClient Client { get; } = fixture.ClientFor(token);
}
```
Note: admin/staff sessions reuse seeded accounts — login directly:
```csharp
public async Task<UserSession> LoginAsync(string email, string password = IntegrationTestFactory.SeedPassword) { /* POST /login, wrap token */ }
```

- [ ] **Step 3: Auth flow tests**

`tests/PriceNegotiationApp.IntegrationTests/AuthFlowShould.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using PriceNegotiationApp.Api.Contracts;
using PriceNegotiationApp.IntegrationTests.Support;

namespace PriceNegotiationApp.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AuthFlowShould(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task Register_login_and_read_current_user()
    {
        var session = await fixture.CreateUserAsync();

        var me = await session.Client.GetAsync("/api/v1/auth/me");

        me.EnsureSuccessStatusCode();
        var user = await me.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.Equal(session.Email, user!.Email);
        Assert.Contains("Customer", user.Roles);
    }

    [Fact]
    public async Task Duplicate_registration_conflicts()
    {
        var email = $"dup.{Guid.NewGuid():N}@test.local";
        var first = await fixture.Anonymous.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest { Email = email, Password = "Passw0rd!" });
        var second = await fixture.Anonymous.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest { Email = email, Password = "Passw0rd!" });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Bad_password_is_unauthorized_with_stable_code()
    {
        var session = await fixture.CreateUserAsync();
        var response = await fixture.Anonymous.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest { Email = session.Email, Password = "WrongPass1!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"code\":\"invalid_credentials\"", body.Replace(" ", string.Empty));
    }

    [Fact]
    public async Task Five_failed_attempts_lock_account()
    {
        var session = await fixture.CreateUserAsync();
        HttpStatusCode last = HttpStatusCode.OK;
        for (var i = 0; i < 6; i++)
        {
            last = (await fixture.Anonymous.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = session.Email, Password = "WrongPass1!" })).StatusCode;
        }

        Assert.Equal(HttpStatusCode.Unauthorized, last);
        var retry = await fixture.Anonymous.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest { Email = session.Email, Password = "Passw0rd!" }); // even correct password now locked
        Assert.Contains("account_locked", await retry.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Me_requires_authentication()
    {
        var response = await fixture.Anonymous.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

- [ ] **Step 4: Validate & commit**

Requires Docker running locally:
```pwsh
dotnet test tests/PriceNegotiationApp.IntegrationTests
git add -A && git commit -m "Add Testcontainers integration harness and end-to-end auth flow tests"
```

---

### Task 11: Products integration matrix

**Files:**
- Test: `tests/PriceNegotiationApp.IntegrationTests/ProductsShould.cs`

- [ ] **Step 1: Tests** (role matrix × routes + filtering/paging/validation)

Cover explicitly:
- Anon can list/get; anon blocked from create/update/delete (401/403).
- Customer blocked from all writes (403).
- Staff can create/update, cannot delete (403); admin can delete (204).
- Missing product → 404 with `product_not_found`.
- Invalid create payload (empty name, negative price) → 400 `validation_failed`-style built-in validation response (assert status + presence of errors array).
- Filtering: create 3 known products; assert `search`, `minPrice`, `maxPrice`, `sortBy=price&sortDesc`, `page/pageSize` behaviors incl. `totalCount`.
- PUT with identical body returns 200 unchanged (idempotent no-op).

Write as one focused test class using `fixture.CreateUserAsync()` + `LoginAsync(IntegrationTestFactory.AdminEmail)` / staff login helpers; ~12 test methods mirroring the bullets above with plain asserts.

- [ ] **Step 2: Validate & commit**

```pwsh
dotnet test tests/PriceNegotiationApp.IntegrationTests
git add -A && git commit -m "Add products API integration matrix: RBAC, validation, filtering, paging"
```

---

### Task 12: Negotiations integration suite

**Files:**
- Test: `tests/PriceNegotiationApp.IntegrationTests/NegotiationsShould.cs`

- [ ] **Step 1: Tests** — the core business suite:

1. `customer_creates_negotiation_within_limit` → 201; `GET mine` shows it; `proposalsRemaining == 2`.
2. `creation_over_double_price_rejected_400` with code `proposal_exceeds_limit`.
3. `double_open_negotiation_conflicts` → 409 `negotiation_already_open`.
4. `full_back_and_forth_then_accept`: create → staff decline → counter (remaining 1) → staff decline → counter (remaining 0) → staff accept → status Accepted; further PATCH → 409 `negotiation_closed`.
5. `budget_exhaustion_yields_409_no_proposals_remaining`: create → decline×2 + counters×2 → third counter attempt → 409 `no_proposals_remaining`.
6. `counter_proposal_over_limit_auto_rejects`: create → PATCH 300 (base 100) → outcome `AutoRejected`, status Declined, decidedAtUtc present.
7. `stranger_cannot_view_negotiation` → 403; `staff_and_admin_can_view` → 200.
8. `only_owner_can_counter_propose` → other customer gets 403.
9. `owner_can_withdraw` → 204; `stranger_cannot_withdraw` → 403; `admin_can_delete_any` → 204.
10. `decline_by_staff_keeps_open_until_budget_spent`: decline → still Open in `GET mine` while remaining > 0.

Helper within the class: create product as staff via `POST /products`, then negotiate against it.

- [ ] **Step 2: Validate & commit**

```pwsh
dotnet test tests/PriceNegotiationApp.IntegrationTests
git add -A && git commit -m "Add negotiation lifecycle integration suite: limits, auto-reject, RBAC, withdrawal"
```

---

### Task 13: Platform — Docker, CI, Dependabot

**Files:**
- Create: `Dockerfile`, `.dockerignore`, `docker-compose.yml`, `.github/workflows/ci.yml`, `.github/dependabot.yml`

- [ ] **Step 1: Dockerfile**

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props PriceNegotiationApp.slnx ./
COPY src src
COPY tests tests
RUN dotnet restore
RUN dotnet publish src/PriceNegotiationApp.Api -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
USER app
HEALTHCHECK --interval=30s --timeout=5s CMD ["/usr/bin/wget", "-qO-", "http://localhost:8080/health/live"]
ENTRYPOINT ["dotnet", "PriceNegotiationApp.Api.dll"]
```
(Note: tests are executed by CI, not baked into image build — keeps image lean; `tests` copy exists because slnx restore needs referenced projects.)

- [ ] **Step 2: docker-compose.yml**

```yaml
services:
  api:
    build: .
    depends_on: [postgres]
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      Database__ConnectionString: Host=postgres;Port=5432;Database=pricenego;Username=postgres;Password=${POSTGRES_PASSWORD:?set}
      Jwt__Issuer: ${JWT_ISSUER:-price-negotiation-app}
      Jwt__Audience: ${JWT_AUDIENCE:-price-negotiation-api}
      Jwt__SecretKey: ${JWT_SECRET_KEY:?set-32+-chars}
      Jwt__ExpiryMinutes: "60"
      Seeding__AdminEmail: ${SEED_ADMIN_EMAIL:-admin@app.com}
      Seeding__AdminPassword: ${SEED_ADMIN_PASSWORD:?set}
      Seeding__StaffEmail: ${SEED_STAFF_EMAIL:-staff@app.com}
      Seeding__StaffPassword: ${SEED_STAFF_PASSWORD:?set}
      Seeding__SeedSampleProducts: "true"
    ports: ["8080:8080"]

  postgres:
    image: postgres:17-alpine
    environment:
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:?set}
      POSTGRES_DB: pricenego
    volumes: [pgdata:/var/lib/postgresql/data]
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 5s
      retries: 10

volumes:
  pgdata:
```
Plus `.env.example` documenting required vars (no values committed).

- [ ] **Step 3: CI workflow**

`.github/workflows/ci.yml`:
```yaml
name: ci
on:
  push:
    branches: [main, develop]
  pull_request:

jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
          cache: true
          cache-dependency-path: '**/packages.lock.json'   # optional; drop if lock files unused
      - run: dotnet restore
      - run: dotnet format --verify-no-changes
      - run: dotnet build -c Release --no-restore
      - run: dotnet test --no-build -c Release --collect:"XPlat Code Coverage"
      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: openapi
          path: artifacts/openapi/
      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: coverage
          path: '**/TestResults/**/coverage.cobertura.xml'
```
(Remove the cache step lines if lock-file mode isn't enabled — simplest correct form omits them.)

- [ ] **Step 4: Dependabot**

`.github/dependabot.yml`:
```yaml
version: 2
updates:
  - package-ecosystem: nuget
    directory: /
    schedule: { interval: weekly }
  - package-ecosystem: github-actions
    directory: /
    schedule: { interval: weekly }
  - package-ecosystem: docker
    directory: /
    schedule: { interval: weekly }
```

- [ ] **Step 5: Validate & commit**

```pwsh
docker compose build
docker compose up -d postgres
$env:POSTGRES_PASSWORD='localpw'; $env:JWT_SECRET_KEY=('x'*40); $env:SEED_ADMIN_PASSWORD='Admin123!'; $env:SEED_STAFF_PASSWORD='Staff123!'
docker compose up api    # expect healthy
docker compose down
git add -A && git commit -m "Add containerized deployment, GitHub Actions CI, Dependabot"
```

---

### Task 14: README + final sweep

**Files:**
- Rewrite: `README.md`
- Verify: full pipeline green

- [ ] **Step 1: README** — rewrite covering: stack summary (drop dead badges/links), architecture diagram-in-text (4 projects), quickstart (compose up + env table, or `dotnet run` + user-secrets commands copied from Task 7), endpoint table matching spec §6, negotiation rules prose (3 proposals total incl. initial, >2× auto-decline, staff accept/decline, withdraw), auth model (register→Customer; staff/admin seeded), config reference, health endpoints, license unchanged.

- [ ] **Step 2: Final sweep**

```pwsh
dotnet format
dotnet build
dotnet test
git add -A && git commit -m "Modernize README and finalize formatting"
```

---

## Self-Review notes (already applied inline)

- Removed all draft/sketch code blocks; every step now contains final, compilable code only.
- Fixed: ProductsModule POST double-call, stray `using` in NegotiationsModule, `IdentityAccountStore` failure branch (named helpers), missing `await` on products list handler, `WebApplicationBuilderExtensions` partial-block duplication, `MapModules`/health-checks sequencing between Tasks 7–9, UnitTests → Infrastructure project reference for `JwtManagerShould`.
- Known deliberate deviations from spec (documented in Global Constraints): idempotent PUT, no client-visible xmin 409 test, CorrelationId enricher dropped.
- Type consistency verified against canonical type map: service signatures ↔ module handlers ↔ exception codes ↔ test assertions.
