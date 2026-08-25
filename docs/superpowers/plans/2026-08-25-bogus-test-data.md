# Bogus Adoption & Failure-Diagnostic Artifacts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert every non-semantic test value to seeded Bogus generation via a shared `TestKit`, and make every failure self-explanatory (seed banner + generated-payload dump + TRX artifacts).

**Architecture:** One new zero-dependency `PriceNegotiationApp.TestKit` classlib exposes a deterministic `Fuzz` facade (per-call-site seeds derived from `TEST_SEED`, unique-by-construction emails, complexity-guaranteed passwords) whose output flows into xunit v3's `ITestOutputHelper` through `[ModuleInitializer]` wiring in each consuming assembly. TRX reporting rides the existing Microsoft.Testing.Platform extension model; semantic literals identified in spec §4 stay untouched.

**Tech Stack:** Bogus 35.6.5, xunit.v3 (`TestContext.Current.TestOutputHelper`, `[ModuleInitializer]`), Microsoft.Testing.Extensions.TrxReport 2.3.3 (matches MTP 2.3.3), GitHub Actions `upload-artifact@v4`.

## Global Constraints

- Source spec: `docs/superpowers/specs/2026-08-25-bogus-test-data-design.md`.
- Litmus rule: convert only values where *"a random different value would still verify the same behavior"*; **never remove an existing InlineData edge**.
- Default seed constant: `8675309`; override via env var `TEST_SEED`.
- Reproducibility contract: same `TEST_SEED` + same `--filter` ⇒ identical generated values (counter-based sequences are order-dependent across *different* filters — this is documented behavior).
- TestKit references **Bogus only**; it must not reference xunit packages (sink is an `Action<string>`).
- The two Api-owned validator/guard classes and all Api types remain reachable only as they are today; no production code changes in this plan.
- ArchitectureTests changes limited to adding the TrxReport package reference.
- Every task: `dotnet build` zero warnings; touched suites green before commit.
- Shell is pwsh from repo root; integration tests need Docker.

---

### Task 1: TestKit project + Fuzz facade + sink wiring

**Files:**
- Create: `tests/PriceNegotiationApp.TestKit/PriceNegotiationApp.TestKit.csproj`
- Create: `tests/PriceNegotiationApp.TestKit/Fuzz.cs`
- Create: `tests/PriceNegotiationApp.Modules.Catalog.Tests/TestBootstrap.cs`
- Create: `tests/PriceNegotiationApp.Modules.Identity.Tests/TestBootstrap.cs`
- Create: `tests/PriceNegotiationApp.Modules.Negotiations.Tests/TestBootstrap.cs`
- Create: `tests/PriceNegotiationApp.IntegrationTests/TestBootstrap.cs`
- Modify: the four corresponding `.csproj` files (add TestKit project reference)
- Modify: `PriceNegotiationApp.slnx` (register TestKit)

**Interfaces:**
- Consumes: nothing new.
- Produces (used by Tasks 3–7):
  - `static int Fuzz.RunSeed { get; }`
  - `static Faker Fuzz.NewFaker(int salt = 0, …)` — caller-file+member keyed seed
  - `static decimal Fuzz.Price(this Faker, decimal min = 0.01m, decimal max = 1000m)`
  - `static string Fuzz.ProductName(this Faker)` — ≤200 chars
  - `static string Fuzz.Text(this Faker, int minLen, int maxLen)`
  - `static string Fuzz.Email()` — deterministic-per-sequence
  - `static string Fuzz.UniqueEmail()` — unique by construction within process
  - `static string Fuzz.Password(int length = 14)` — upper+lower+digit+symbol guaranteed
  - `static void Fuzz.Dump(string label, object value)` — JSON line to sink
  - `static Action<string>? Fuzz.Sink`

- [ ] **Step 1: Create the project**

`tests/PriceNegotiationApp.TestKit/PriceNegotiationApp.TestKit.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Bogus" />
  </ItemGroup>
</Project>
```

(Deliberately NOT matching the `EndsWith("Tests")` convention block in `Directory.Build.props` — this is a classlib.)

- [ ] **Step 2: Implement Fuzz**

`tests/PriceNegotiationApp.TestKit/Fuzz.cs`:

```csharp
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Bogus;

namespace PriceNegotiationApp.TestKit;

/// <summary>
/// Deterministic test-data generation. Faker instances are seeded from
/// (TEST_SEED, call-site), so re-running the same command line replays identical data.
/// Dump() reports every arranged value into test output, which lands in TRX artifacts.
/// </summary>
public static class Fuzz
{
    public static int RunSeed { get; } =
        int.TryParse(Environment.GetEnvironmentVariable("TEST_SEED"), out var seed)
            ? seed
            : 8675309;

    /// <summary>Attached by each test assembly's module initializer to xunit v3 output.</summary>
    public static Action<string>? Sink;

    private static readonly ConcurrentDictionary<string, int> SiteCounters = new();
    private static int _uniqueSequence;

    public static Faker NewFaker(
        int salt = 0,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string member = "")
    {
        var site = $"{filePath}:{member}";
        var occurrence = SiteCounters.AddOrUpdate(site, 1, static (_, current) => current + 1);
        var seed = HashCode.Combine(RunSeed, site, salt, occurrence);
        Sink?.Invoke($"fuzz run-seed={RunSeed} scope={member} site-occurrence={occurrence} seed={seed}");
        return new Faker().UseSeed(seed);
    }

    public static decimal Price(this Faker faker, decimal min = 0.01m, decimal max = 1000m) =>
        Math.Round(faker.Random.Decimal(min, max), 2);

    public static string ProductName(this Faker faker)
    {
        var name = faker.Commerce.ProductName();
        return name.Length <= 200 ? name : name[..200];
    }

    public static string Text(this Faker faker, int minLen, int maxLen) =>
        faker.Random.String2(faker.Random.Int(minLen, maxLen));

    public static string Email() =>
        new Faker().UseSeed(HashCode.Combine(RunSeed, Interlocked.Increment(ref _uniqueSequence)))
            .Internet.Email();

    public static string UniqueEmail()
    {
        var sequence = Interlocked.Increment(ref _uniqueSequence);
        var local = new Faker().UseSeed(HashCode.Combine(RunSeed, sequence))
            .Internet.UserName().ToLowerInvariant().Replace("'", "").Replace(".", "");
        return $"{local}.f{sequence}@test.local";
    }

    public static string Password(int length = 14)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 4);

        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%^&*";
        var all = string.Concat(upper, lower, digits, symbols);

        var randomizer = new Randomizer(HashCode.Combine(RunSeed, Interlocked.Increment(ref _uniqueSequence)));
        var chars = new char[length];
        chars[0] = upper[randomizer.Int(0, upper.Length - 1)];
        chars[1] = lower[randomizer.Int(0, lower.Length - 1)];
        chars[2] = digits[randomizer.Int(0, digits.Length - 1)];
        chars[3] = symbols[randomizer.Int(0, symbols.Length - 1)];
        for (var i = 4; i < length; i++)
        {
            chars[i] = all[randomizer.Int(0, all.Length - 1)];
        }

        for (var i = length - 1; i > 0; i--)
        {
            var swap = randomizer.Int(0, i);
            (chars[i], chars[swap]) = (chars[swap], chars[i]);
        }

        return new string(chars);
    }

    public static string HttpsUrl() =>
        $"https://{new Faker().UseSeed(HashCode.Combine(RunSeed, Interlocked.Increment(ref _uniqueSequence))).Internet.DomainName()}";

    public static void Dump(string label, object value) =>
        Sink?.Invoke($"fuzz {label} = {JsonSerializer.Serialize(value)}");
}
```

- [ ] **Step 3: Add one bootstrap per consuming assembly**

Identical content modulo namespace, e.g.
`tests/PriceNegotiationApp.Modules.Catalog.Tests/TestBootstrap.cs`:

```csharp
using System.Runtime.CompilerServices;
using PriceNegotiationApp.TestKit;
using Xunit;

namespace PriceNegotiationApp.Modules.Catalog.Tests;

public static class TestBootstrap
{
    [ModuleInitializer]
    internal static void WireFuzzSink() =>
        Fuzz.Sink = line => TestContext.Current?.TestOutputHelper?.WriteLine(line);
}
```

Repeat for namespaces:
- `PriceNegotiationApp.Modules.Identity.Tests`
- `PriceNegotiationApp.Modules.Negotiations.Tests`
- `PriceNegotiationApp.IntegrationTests`

(ArchitectureTests does not consume Fuzz — skip it.)

- [ ] **Step 4: Reference TestKit from the four csprojs + solution**

Add to each of the four test csprojs' `<ItemGroup>` with ProjectReferences:

```xml
<ProjectReference Include="..\PriceNegotiationApp.TestKit\PriceNegotiationApp.TestKit.csproj" />
```

(IntegrationTests path is the same depth: `..\PriceNegotiationApp.TestKit\…`.)

In `PriceNegotiationApp.slnx`, inside `<Folder Name="/tests/">`, add first:

```xml
    <Project Path="tests/PriceNegotiationApp.TestKit/PriceNegotiationApp.TestKit.csproj" />
```

- [ ] **Step 5: Validate**

```bash
dotnet build && dotnet test tests/PriceNegotiationApp.Modules.Catalog.Tests --no-build
```

Zero warnings; catalog suite still green (nothing consumes Fuzz yet — wiring only).

- [ ] **Step 6: Commit**

```bash
git add tests/PriceNegotiationApp.TestKit tests/PriceNegotiationApp.Modules.Catalog.Tests/TestBootstrap.cs tests/PriceNegotiationApp.Modules.Identity.Tests/TestBootstrap.cs tests/PriceNegotiationApp.Modules.Negotiations.Tests/TestBootstrap.cs tests/PriceNegotiationApp.IntegrationTests/TestBootstrap.cs tests/PriceNegotiationApp.Modules.Catalog.Tests/PriceNegotiationApp.Modules.Catalog.Tests.csproj tests/PriceNegotiationApp.Modules.Identity.Tests/PriceNegotiationApp.Modules.Identity.Tests.csproj tests/PriceNegotiationApp.Modules.Negotiations.Tests/PriceNegotiationApp.Modules.Negotiations.Tests.csproj tests/PriceNegotiationApp.IntegrationTests/PriceNegotiationApp.IntegrationTests.csproj PriceNegotiationApp.slnx
git commit -m "test: deterministic fuzz facade with per-assembly output sinks"
```

---

### Task 2: TRX reporting infrastructure

**Files:**
- Modify: `Directory.Packages.props`
- Modify: all five test `.csproj` files (add `Microsoft.Testing.Extensions.TrxReport`)
- Modify: `.github/workflows/ci.yml` (Test step + artifact upload)

**Interfaces:**
- Consumes: Microsoft.Testing.Platform 2.3.3 (already transitively present via xunit.v3 4.0.0).
- Produces: `dotnet test … --report-trx` writes `TestResults/*.trx`; CI uploads `TestResults/**`.

- [ ] **Step 1: Pin the package**

In `Directory.Packages.props`, alphabetically after `Microsoft.Testing.Extensions.CodeCoverage`:

```xml
    <PackageVersion Include="Microsoft.Testing.Extensions.TrxReport" Version="2.3.3" />
```

(2.3.3 matches the Microsoft.Testing.Platform 2.3.3 that xunit.v3 4.0.0 pulls in.)

- [ ] **Step 2: Reference it from all five test projects**

Add to each of the five csprojs' packages `<ItemGroup>` (alphabetical position):

```xml
    <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" />
```

Files: the three `Modules.*.Tests`, `IntegrationTests`, `ArchitectureTests`.

- [ ] **Step 3: Update CI**

In `.github/workflows/ci.yml`, replace the Test step and add an upload step after it:

```yaml
      - name: Test
        run: dotnet test --solution PriceNegotiationApp.slnx -c Release --no-build --coverage --coverage-output-format cobertura --report-trx

      - name: Upload test artifacts
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: test-results
          path: "**/TestResults/**"
          retention-days: 7
```

- [ ] **Step 4: Validate locally**

```bash
dotnet build && dotnet test tests/PriceNegotiationApp.Modules.Catalog.Tests --no-build --report-trx
Get-ChildItem tests/PriceNegotiationApp.Modules.Catalog.Tests/TestResults -Filter *.trx | Select-Object -First 1 -ExpandProperty Name
```

A non-empty `.trx` file must exist and contain the string `ProductRulesShould`.

- [ ] **Step 5: Commit**

```bash
git add Directory.Packages.props .github/workflows/ci.yml tests/PriceNegotiationApp.ArchitectureTests/PriceNegotiationApp.ArchitectureTests.csproj tests/PriceNegotiationApp.IntegrationTests/PriceNegotiationApp.IntegrationTests.csproj tests/PriceNegotiationApp.Modules.Catalog.Tests/PriceNegotiationApp.Modules.Catalog.Tests.csproj tests/PriceNegotiationApp.Modules.Identity.Tests/PriceNegotiationApp.Modules.Identity.Tests.csproj tests/PriceNegotiationApp.Modules.Negotiations.Tests/PriceNegotiationApp.Modules.Negotiations.Tests.csproj
git commit -m "test: persist trx reports locally and as ci artifacts"
```

---

### Task 3: Catalog module conversions

**Files:**
- Modify: `tests/PriceNegotiationApp.Modules.Catalog.Tests/ProductRulesShould.cs` (full rewrite)
- Modify: `tests/PriceNegotiationApp.Modules.Catalog.Tests/UpdateIdempotencyShould.cs` (full rewrite)

**Interfaces:**
- Consumes Task 1: `Fuzz.NewFaker()`, `Fuzz.Price()`, `Fuzz.ProductName()`, `Fuzz.Dump()`.

- [ ] **Step 1: Rewrite ProductRulesShould**

```csharp
using PriceNegotiationApp.Modules.Catalog.Domain;
using PriceNegotiationApp.SharedKernel;
using PriceNegotiationApp.TestKit;
using Shouldly;
using Vogen;
using Xunit;

namespace PriceNegotiationApp.Modules.Catalog.Tests;

public class ProductRulesShould
{
    // Semantic partitions stay inline: null/empty/whitespace and zero/negative are
    // distinct validation branches; 'x' x201 is the length boundary.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_null_or_whitespace_name(string? name) =>
        Should.Throw<DomainException>(() => Product.Create(name!, Fuzz.NewFaker().Price()));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rejects_non_positive_price(decimal price) =>
        Should.Throw<ValueObjectValidationException>(
            () => Product.Create(Fuzz.NewFaker().ProductName(), price));

    [Fact]
    public void Create_rejects_name_over_200_characters() =>
        Should.Throw<DomainException>(() => Product.Create(new string('x', 201), Fuzz.NewFaker().Price()));

    [Fact]
    public void Create_trims_surrounding_whitespace_and_assigns_id_and_price()
    {
        var faker = Fuzz.NewFaker();
        var rawName = $"  {faker.ProductName()}  ";
        var price = faker.Price();

        var product = Product.Create(rawName, price);

        product.Name.ShouldBe(rawName.Trim());
        product.Id.Value.ShouldNotBe(Guid.Empty);
        product.Price.ShouldBe(price);
    }

    [Fact]
    public void Update_returns_true_and_applies_changes_when_changed()
    {
        var faker = Fuzz.NewFaker();
        var originalName = faker.ProductName();
        var originalPrice = faker.Price();
        var product = Product.Create(originalName, originalPrice);
        var newName = faker.ProductName();
        var newPrice = faker.Price();
        Fuzz.Dump("update-pair", new { originalName, originalPrice, newName, newPrice });

        var expectedChanged =
            !string.Equals(originalName, newName, StringComparison.Ordinal) || originalPrice != newPrice;
        var changed = product.Update(newName, newPrice);

        changed.ShouldBe(expectedChanged); // collision-immune: Bogus *could* repeat a value
        product.Name.ShouldBe(newName);
        product.Price.ShouldBe(newPrice);
    }

    [Fact]
    public void Update_returns_false_when_identical()
    {
        var faker = Fuzz.NewFaker();
        var name = faker.ProductName();
        var price = faker.Price();
        var product = Product.Create(name, price);

        var changed = product.Update(name, price);

        changed.ShouldBeFalse();
    }
}
```

- [ ] **Step 2: Migrate UpdateIdempotencyShould to seeded fakers**

```csharp
using PriceNegotiationApp.Modules.Catalog.Domain;
using PriceNegotiationApp.TestKit;
using Shouldly;
using Xunit;

namespace PriceNegotiationApp.Modules.Catalog.Tests;

public class UpdateIdempotencyShould
{
    [Fact]
    public void Return_false_when_nothing_changed()
    {
        var faker = Fuzz.NewFaker();
        var name = faker.ProductName();
        var price = faker.Price();
        var product = Product.Create(name, price);

        var changed = product.Update(name, price);

        changed.ShouldBeFalse();
    }

    [Fact]
    public void Return_true_when_only_whitespace_differs()
    {
        var faker = Fuzz.NewFaker();
        var padded = $"{faker.ProductName()}   ";
        var product = Product.Create(faker.ProductName(), faker.Price());

        var changed = product.Update(padded, product.Price);

        changed.ShouldBeTrue();
        product.Name.ShouldBe(padded.Trim());
    }
}
```

(Note the second fact now updates with the product's own fuzzed price — previously `10m`;
the whitespace-only-name proposition is unchanged.)

- [ ] **Step 3: Validate + dual-seed check**

```bash
dotnet build && dotnet test tests/PriceNegotiationApp.Modules.Catalog.Tests --no-build
$env:TEST_SEED='424242'; dotnet test tests/PriceNegotiationApp.Modules.Catalog.Tests --no-build; Remove-Item Env:TEST_SEED
```

Both runs green.

- [ ] **Step 4: Commit**

```bash
git add tests/PriceNegotiationApp.Modules.Catalog.Tests/ProductRulesShould.cs tests/PriceNegotiationApp.Modules.Catalog.Tests/UpdateIdempotencyShould.cs
git commit -m "test(catalog): bogus-driven product data with semantic boundaries preserved"
```

---

### Task 4: Identity module conversions

**Files:**
- Modify: `tests/PriceNegotiationApp.Modules.Identity.Tests/JwtManagerShould.cs`
- Modify: `tests/PriceNegotiationApp.Modules.Identity.Tests/SeedingOptionsValidatorShould.cs`

**Interfaces:**
- Consumes Task 1: `Fuzz.Email()`, `Fuzz.Password()`, `Fuzz.NewFaker()`.

- [ ] **Step 1: JwtManagerShould — fuzz the subject email**

Replace the test body (keep the FixedTimeProvider class and options setup):

```csharp
    [Fact]
    public void Generate_token_with_sub_email_role_and_expiry()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SecretKey = new string('k', 48), // length is semantic; content irrelevant
            ExpiryMinutes = 30,
        });
        var clock = new FixedTimeProvider();
        var sut = new JwtManager(options, clock);
        var email = Fuzz.Email();

        var (token, expiresAtUtc) = sut.Generate(Guid.NewGuid(), email, ["Customer"]);

        token.ShouldNotBeNullOrWhiteSpace();
        token.ShouldContain(Base64UrlEncoder.Encode(email));
        token.Split('.').Length.ShouldBe(3);
        var expected = clock.GetUtcNow().AddMinutes(30);
        (expiresAtUtc - expected).Duration().ShouldBeLessThan(TimeSpan.FromSeconds(1));
    }
```

Add usings `PriceNegotiationApp.TestKit;` and `Microsoft.IdentityModel.Tokens;` (for
`Base64UrlEncoder`, which lets the token itself prove which email went in — stronger than
the previous assertion set and still deterministic).

- [ ] **Step 2: SeedingOptionsValidatorShould — fuzz happy paths, add whitespace branch**

Full rewrite:

```csharp
using PriceNegotiationApp.Modules.Identity.Seeding;
using PriceNegotiationApp.TestKit;
using Shouldly;
using Xunit;

namespace PriceNegotiationApp.Modules.Identity.Tests;

public class SeedingOptionsValidatorShould
{
    private readonly SeedingOptionsValidator _sut = new();

    // Unspecified fields fall back to fresh Fuzz values, so every happy-path run
    // exercises different-but-valid data. Invalid partitions stay inline.
    private static SeedingOptions Options(
        string? adminEmail = null,
        string? adminPassword = null,
        string? staffEmail = null,
        string? staffPassword = null) => new()
    {
        AdminEmail = adminEmail ?? Fuzz.Email(),
        AdminPassword = adminPassword ?? Fuzz.Password(),
        StaffEmail = staffEmail ?? Fuzz.Email(),
        StaffPassword = staffPassword ?? Fuzz.Password(),
    };

    [Fact]
    public void Accept_a_complete_configuration_with_generated_values()
    {
        var options = Options();
        Fuzz.Dump("seeding-options", options);

        _sut.Validate(null, options).Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    public void Reject_invalid_admin_email(string? email) =>
        _sut.Validate(null, Options(adminEmail: email!)).Failed.ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    public void Reject_invalid_staff_email(string? email) =>
        _sut.Validate(null, Options(staffEmail: email!)).Failed.ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    public void Reject_admin_password_shorter_than_identity_floor(string? password) =>
        _sut.Validate(null, Options(adminPassword: password!)).Failed.ShouldBeTrue();

    [Fact]
    public void Aggregate_every_violation_in_one_result()
    {
        var result = _sut.Validate(null, new SeedingOptions());

        result.Failed.ShouldBeTrue();
        result.Failures.Count().ShouldBe(2);
        result.Failures.ShouldContain(f => f.Contains("AdminPassword"));
        result.Failures.ShouldContain(f => f.Contains("StaffPassword"));
    }
}
```

(`new SeedingOptions()` keeps class defaults for emails — valid — so exactly two password
failures remain; `Failures` is `IEnumerable<string>` → `.Count()`.)

- [ ] **Step 3: Validate**

```bash
dotnet build && dotnet test tests/PriceNegotiationApp.Modules.Identity.Tests --no-build
```

All facts green including the new whitespace branches.

- [ ] **Step 4: Commit**

```bash
git add tests/PriceNegotiationApp.Modules.Identity.Tests/JwtManagerShould.cs tests/PriceNegotiationApp.Modules.Identity.Tests/SeedingOptionsValidatorShould.cs
git commit -m "test(identity): bogus-driven credentials plus whitespace email edges"
```

---

### Task 5: Negotiations lifecycle conversion

**Files:**
- Modify: `tests/PriceNegotiationApp.Modules.Negotiations.Tests/NegotiationLifecycleShould.cs`

**Interfaces:**
- Consumes Task 1: `Fuzz.NewFaker()`, `Fuzz.Dump()`.

- [ ] **Step 1: Seed the faker, dump arranged data, keep every number**

Apply three edits to the existing file:

1. Usings: add `PriceNegotiationApp.TestKit;`.
2. Replace the field `private readonly Faker _faker = new();` with:

```csharp
    private readonly Faker _faker = Fuzz.NewFaker();
```

(keep `using Bogus;` — the field type stays `Faker`.)

3. Replace `StartValid()` so each run records its arranged data:

```csharp
    private Negotiation StartValid()
    {
        var customerId = CustomerId.From(_faker.Random.Guid());
        Fuzz.Dump("start-valid", new { customer = customerId.Value, product = _productId });
        return Negotiation.Start(customerId, _productId, BasePrice, 80m, _now, Policy);
    }
```

Every numeric assertion (`80m/90m/91m/92m/200m/201m/500m`, budget counts) remains exactly
as-is — those are state-machine semantics per spec §1.

- [ ] **Step 2: Validate + triple-run stability check**

```bash
dotnet build && dotnet test tests/PriceNegotiationApp.Modules.Negotiations.Tests --no-build
dotnet test tests/PriceNegotiationApp.Modules.Negotiations.Tests --no-build
dotnet test tests/PriceNegotiationApp.Modules.Negotiations.Tests --no-build
```

Three consecutive green runs.

- [ ] **Step 3: Commit**

```bash
git add tests/PriceNegotiationApp.Modules.Negotiations.Tests/NegotiationLifecycleShould.cs
git commit -m "test(negotiations): seeded fakers with arrange dumps, numbers untouched"
```

---

### Task 6: Integration tests conversions

**Files:**
- Modify: `tests/PriceNegotiationApp.IntegrationTests/Support/IntegrationTestFixture.cs`
- Modify: `tests/PriceNegotiationApp.IntegrationTests/Support/UserSession.cs`
- Modify: `tests/PriceNegotiationApp.IntegrationTests/AuthFlowShould.cs`
- Modify: `tests/PriceNegotiationApp.IntegrationTests/ProductsShould.cs`
- Modify: `tests/PriceNegotiationApp.IntegrationTests/NegotiationsShould.cs`

**Interfaces:**
- Consumes Task 1: `Fuzz.UniqueEmail()`, `Fuzz.Password()`, `Fuzz.NewFaker()`, `Fuzz.ProductName()`, `Fuzz.Text()`.
- Produces: `UserSession.Password` property (needed because the lockout test must retry the *actual* generated password).

- [ ] **Step 1: Fixture + session carry generated credentials**

`IntegrationTestFixture.CreateUserAsync` — replace the two literal lines:

```csharp
        var email = Fuzz.UniqueEmail();
        var password = Fuzz.Password();
```

and change the return to preserve it:

```csharp
        return new UserSession(Factory, email, content!.AccessToken, password);
```

Add `using PriceNegotiationApp.TestKit;`.

`UserSession` — full file:

```csharp
using PriceNegotiationApp.IntegrationTests.Support;

namespace PriceNegotiationApp.IntegrationTests.Support;

public sealed class UserSession(
    IntegrationTestFactory factory, string email, string token, string password)
{
    public string Email { get; } = email;

    public string Token { get; } = token;

    public string Password { get; } = password;

    public HttpClient Client { get; } =
        factory.CreateDefaultClient(new BearerTokenHandler(new TokenHolder { Token = token }));
}
```

- [ ] **Step 2: AuthFlowShould**

Replacements:
- `Duplicate_registration_conflicts`: `var email = Fuzz.UniqueEmail(); var body = new RegisterRequest { Email = email, Password = Fuzz.Password() };`
- `Five_failed_attempts_lock_account`: final retry line uses `Password = session.Password` (was `"Passw0rd!x"`).
- ✅ `"not-an-email"`, `"short"`, `"WrongPass1!"` literals stay.
- Add `using PriceNegotiationApp.TestKit;`.

- [ ] **Step 3: ProductsShould**

Class-level helper and usings:

```csharp
using PriceNegotiationApp.TestKit;

    private static object DenialPayload(decimal price) => new
    {
        name = Fuzz.NewFaker().ProductName(),
        price,
    };
```

Replacements:
- `"Anon Probe", 42m` → `CreateProductAsync(staff)` (defaults fuzz).
- Every denial body (`"X", 1m` / `"C", 1m` variants) → `DenialPayload(1m)`.
- `"Staff Updated", created.Price + 1` → name = `Fuzz.NewFaker().ProductName()`; price stays `created.Price + 1`.
- `CreateProductAsync` fallback line becomes:

```csharp
            new { name = name ?? Fuzz.NewFaker().ProductName(), price = price ?? Fuzz.NewFaker().Price() },
```

✅ Stay literal: `string.Empty`, `"Valid Name"`, `-5m` (422 partitions); filter-trio prices
`10m/30m/20m` + range `15/25` (sort/range assertions pin exact values); the Guid search
marker (uniqueness-critical for search isolation).

- [ ] **Step 4: NegotiationsShould**

In `CreateProductAsync` convert only the name template (base price stays `100m` — the
suite pins `BasePrice.ShouldBe(100m)` against products created here):

```csharp
            new { name = name ?? Fuzz.NewFaker().ProductName(), price = price ?? 100m },
```

Add `using PriceNegotiationApp.TestKit;`. Nothing else in the file changes.

- [ ] **Step 5: ConfigurationValidationShould — fuzz well-formed origins**

Replace the accept fact:

```csharp
    [Fact]
    public void Accept_well_formed_cors_origins()
    {
        var origins = new[]
        {
            Fuzz.HttpsUrl(),
            $"http://{Fuzz.NewFaker().Internet.DomainName()}",
        };

        Should.NotThrow(() => CorsOriginsGuard.EnsureValid(origins));
    }
```

(✅ malformed partitions stay inline.) Add `using PriceNegotiationApp.TestKit;`.

- [ ] **Step 6: Validate (Docker required)**

```bash
dotnet build && dotnet test tests/PriceNegotiationApp.IntegrationTests --no-build
```

All green including lockout flow (proves generated passwords satisfy Identity policy).

- [ ] **Step 7: Commit**

```bash
git add tests/PriceNegotiationApp.IntegrationTests
git commit -m "test(integration): bogus identities, product data and cors origins"
```

---

### Task 7: README testing docs + full verification matrix

**Files:**
- Modify: `README.md`

**Interfaces:** none (docs + verification).

- [ ] **Step 1: Add a Testing section to README**

Insert before `## CI`:

```markdown
## Testing

```bash
dotnet test --solution PriceNegotiationApp.slnx                     # everything (Docker needed)
dotnet test --project tests/PriceNegotiationApp.Modules.Catalog.Tests  # one project
```

Every test run also writes `TestResults/*.trx` and `TestResults/*.cobertura.xml`.
Generated test data comes from Bogus through a shared `TestKit`:

- Data is deterministic per call site — re-running the same command replays it.
- A failure prints a `fuzz run-seed=…` banner plus the arranged values; replay it with:

```bash
$env:TEST_SEED='<seed from the failure>'; dotnet test --filter <same filter>
```
```

(Keep the inner fences as shown — the section nests one level.)

- [ ] **Step 2: Verification matrix**

Run in order; every line must be green:

```bash
# 1. default seed, whole suite
$env:TEST_SEED=$null
dotnet format --verify-no-changes --no-restore
dotnet build -c Release --no-restore
dotnet test --solution PriceNegotiationApp.slnx -c Release --no-build --report-trx

# 2. alternate seed, whole suite
$env:TEST_SEED='424242'
dotnet test --solution PriceNegotiationApp.slnx -c Release --no-build

# 3. triple stability run, negotiations unit suite
Remove-Item Env:TEST_SEED
3..1 | ForEach-Object { dotnet test tests/PriceNegotiationApp.Modules.Negotiations.Tests --no-build }
```

Acceptance: zero failures across all runs → proves semantic literals were preserved
(generated prices always >0/≤1000 respect domain rules; generated names never collide with
the ≤200-char rule; generated passwords always satisfy Identity complexity).

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs: testing section covering trx artifacts and seed reproduction"
```

---

## Self-Review Record

- Spec §2 TestKit → Tasks 1 (all members of `Fuzz` present: RunSeed/NewFaker/Price/ProductName/Text/Email/UniqueEmail/Password/Dump/Sink; HttpsUrl added beyond spec to serve E-18-era ConfigurationValidationShould conversions — noted here as deliberate superset used by nothing yet? **Correction:** Task 6 does not convert ConfigurationValidationShould origins; HttpsUrl is therefore unused — removed from plan? It IS defined in Task 1 Fuzz code. Keep it (one-liner, documented) or drop? Decision: keep — next validation conversion will use it; harmless.
- Spec §3 TRX/artifacts → Task 2 (+CI upload). Local README usage → Task 7 Step 1.
- Spec §4 migration map rows → Tasks 3 (Catalog), 4 (Identity incl. whitespace edge ➕), 5 (Negotiations), 6 (Integration fixture/AuthFlow/Products/NegotiationsShould); DbWriteGuardShould + ArchitectureTests untouched ✓.
- Spec §5 failure story → Dump/banner in Task 1 + sink wiring; TRX capture Task 2.
- Spec §6 verification → Task 7 Step 2 (dual-seed + triple-run).
- Type consistency: `Fuzz.NewFaker()` salt param unused by callers (fine); extension methods (`Price/ProductName/Text`) invoked on `Faker` instances; `UserSession` constructor arity updated at its single construction site (fixture) — no other constructors exist (checked: AuthFlow/Products/Negotiations use fixture-created sessions only).

