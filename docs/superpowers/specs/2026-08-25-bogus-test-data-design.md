# Bogus Test Data Adoption & Failure-Diagnostic Artifacts — Design

Date: 2026-08-25
Scope: all five test projects. Goal: every non-semantic test value comes from Bogus,
every generated value is recorded so failures explain themselves, and failed runs are
reproducible byte-for-byte via a seed.

---

## 1. Doctrine — what stays literal, what becomes Bogus

| Kind | Definition | Rule |
|---|---|---|
| **Semantic literal** | The specific value decides the outcome | Stays inline. Prices at boundaries (`0`, `-1`, `200m` exactly-at-limit, `201m` one-over), empty/null/whitespace partitions, lockout threshold `5`, status/error-code strings |
| **Arbitrary instance** | Any valid value proves the same proposition | **Bogus.** Names, valid emails, descriptions, denial payloads (`"X"`, `"C"`), jwt subject email |
| **Unique-constrained** | Must not repeat within a run (DB unique indexes, duplicate-registration tests) | **Bogus + monotonic suffix** (`UniqueEmail()`), never raw `Guid` string-mashing |
| **Pattern-value** | Shape matters, content does not | Either a documented literal (`'k'×48` JWT secret — length is the point) or a shaped Bogus call |

Litmus test used in review: *"If this value were randomly different, would the test still
verify exactly the same behavior?"* Yes → Bogus. No → keep the literal and it should be
obvious from the test name why that value is special.

## 2. TestKit — one small shared project

New `tests/PriceNegotiationApp.TestKit/PriceNegotiationApp.TestKit.csproj`
(classlib, `net10.0`, references **Bogus only** — no xunit dependency). Referenced by all
five existing test projects.

```csharp
public static class Fuzz
{
    public static int RunSeed { get; }          // TEST_SEED env or 8675309
    public static Faker NewFaker(string scope); // UseSeed(stable hash of scope+counter)
    public static decimal Price(...);           // positive, 2dp
    public static string ProductName();         // Commerce.ProductName, clamped ≤ 200 chars
    public static string Email();
    public static string UniqueEmail();         // Email + monotonic suffix
    public static string Password(int len=14);  // upper+lower+digit+symbol guaranteed
    public static string Text(int minLen, int maxLen);
    public static void Dump(string label, object value);  // JSON line -> Sink
    public static Action<string>? Sink;
}
```

- **Determinism:** `UseSeed` derives from `RunSeed` + scope name + per-process counter —
  identical command lines produce identical data; parallel collections never share a Faker.
- **Reproduction:** run a failing test again with `TEST_SEED=<seed from its output>` and
  the same `--filter` — every generated value repeats exactly.
- **Visibility:** each test assembly carries a tiny `[ModuleInitializer]` bootstrap that
  sets `Fuzz.Sink = line => TestContext.Current?.TestOutputHelper?.WriteLine(line)`,
  so dumps flow into xunit/MTP output (and therefore into TRX files) without any
  per-test boilerplate beyond calling `Dump` once after arranging data.

## 3. Failure artifacts — where results live

- All five test projects add `Microsoft.Testing.Extensions.CodeCoverage`
  *(already present)* **and** `Microsoft.Testing.Extensions.TrxReport`.
- Local + CI test invocations append `--report-trx`; MTP writes
  `TestResults/<guid>.trx` per assembly containing: test results, captured output
  (seeds + Fuzz dumps + Shouldly actual-vs-expected), duration, failure messages.
- CI gains an `upload-artifact@v4` step publishing `TestResults/**`
  (TRX + cobertura) with 7-day retention — click a failed CI run, download, open the TRX,
  read the exact generated data.
- README testing section documents: how to run with TRX, how to read `TEST_SEED` from a
  failure, how to reproduce.

## 4. Per-file migration map

Legend: 🔄 convert to Bogus · ✅ keep as-is · ➕ new case

| File | Changes |
|---|---|
| `Modules.Catalog.Tests/ProductRulesShould.cs` | 🔄 `"Keyboard"`, `"Old"→"New"`, `"Same"`, `"Thing"`, `10m/20m/99.5m` → `Fuzz.ProductName()/Price()`; trimming fact pads a Fuzz name with spaces and asserts equality against `.Trim()` ✅ all InlineData partitions (null/""/"   ", 0/-1, 201-char) stay |
| `Modules.Catalog.Tests/UpdateIdempotencyShould.cs` | ✅ already Bogus — migrate `new Faker()` → `Fuzz.NewFaker(scope)` + `Dump` the pair |
| `Modules.Identity.Tests/JwtManagerShould.cs` | 🔄 `"user@test.dev"` → `Fuzz.Email()` ✅ roles `["Customer"]`, secret `'k'×48` stay |
| `Modules.Identity.Tests/SeedingOptionsValidatorShould.cs` | 🔄 happy-path `Options()` defaults → `Fuzz.Email()/Fuzz.Password()`; ➕ whitespace-only email theory case `"   "` (validator uses IsNullOrWhiteSpace — currently untested branch) ✅ null/empty/`not-an-email`/`short` partitions stay |
| `Modules.Negotiations.Tests/NegotiationLifecycleShould.cs` | ✅ every number stays (state-machine semantics); 🔄 `_faker = new()` → `Fuzz.NewFaker(scope)`; `Dump(customerId, offers…)` once per arrange |
| `Modules.Negotiations.Tests/DbWriteGuardShould.cs` | ✅ untouched (pure exception plumbing, constraint name is semantic) |
| `IntegrationTests/AuthFlowShould.cs` | 🔄 `"dup.{guid}@"` → `Fuzz.UniqueEmail()`; `"Passw0rd!x"` literals → `Fuzz.Password()` captured in a variable and Dump-ed ✅ `"not-an-email"`, `"short"`, `"WrongPass1!"` stay |
| `IntegrationTests/ProductsShould.cs` | 🔄 `"Anon Probe"`, `"X"`, `"C"`, `"Staff Updated"`, standalone `1m/2m/42m` → Fuzz; ✅ `created.Price + 1` stays relative |
| `IntegrationTests/NegotiationsShould.cs` | 🔄 `"NegProduct{guid}"[..20]` → `Fuzz.ProductName()` ✅ base `100m` and all offer values stay |
| `IntegrationTests/Support/IntegrationTestFixture.cs` | 🔄 `CreateUserAsync` email template → `Fuzz.UniqueEmail()`; password → `Fuzz.Password()` (stored on session for reuse) |
| `IntegrationTests/ConfigurationValidationShould.cs` | 🔄 well-formed origin URLs → `Fuzz` URL helper (scheme https + random host) ✅ malformed partitions stay |
| `ArchitectureTests/*` | ✅ untouched |

Rule enforced during implementation: **a converted test may not lose an InlineData edge**
— conversion targets only arbitrary instances.

## 5. Error handling / failure story

Failed test output contains, in order: `Fuzz` seed banner (`run-seed=… scope=…`),
the JSON dump of arranged values, then the Shouldly failure (actual vs expected).
Nothing else changes about error propagation.

## 6. Testing the change itself

- Full suite green in both modes: default seed and `TEST_SEED=<other>` (proves no hidden
  coupling to the constant).
- One deliberate flake-check: run Negotiations suite 3× consecutively with different seeds —
  all green (proves semantic literals were correctly preserved and Bogus values respect
  domain constraints, e.g., generated prices always > 0, generated names ≤ 200 chars).
- CI parity: format check, Release build, full MTP suite with TRX + cobertura artifacts.

## 7. Non-goals

Property-based frameworks (CsCheck/FsCheck), shrinking/minimization, golden-file
snapshots, fuzzing concurrency paths, changing production code.
