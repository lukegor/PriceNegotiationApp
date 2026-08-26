# Security Review & Portfolio-Grade Hardening — Design

- Date: 2026-08-26
- Status: Approved (pending implementation)
- Scope decision: Report + fixes, portfolio-grade hardening bar, tests + full-suite verification.

## Goal

Prove the codebase's security posture and close the gaps an interviewer could spot or
exploit in a live demo. The review artifact doubles as portfolio evidence: every OWASP
category ends with an explicit verdict, and deliberate non-builds are documented as
decisions rather than oversights.

## Method

1. **OWASP-mapped checklist** over: authentication, authorization (incl. object-level
   access), injection, security misconfiguration, secrets handling, transport security,
   logging/telemetry exposure, resource limits/DoS, supply chain, container/compose/CI.
   Each category gets `checked` / `finding(s)` / `N/A` with evidence.
2. **Adversarial route sweep**: all 17 API routes re-checked pairwise for
   role attribute + object-ownership correctness (IDOR, mass assignment, privilege
   mixing).
3. **Supply chain**: `dotnet list package --vulnerable --include-transitive`, plus
   review of NuGetAudit config, Dependabot config, and CI pinning.

## Deliverables

1. Findings report embedded in this doc's "Findings" section after execution:
   severity / evidence (`file:line`) / recommendation / status.
2. Implemented fixes in risk-ordered batches, each batch ending green on the full suite.
3. A "Deliberate trade-offs" section documenting intentionally unbuilt machinery.

## Candidate findings (pre-identified, to be confirmed during execution)

| # | Candidate | Evidence | Planned fix |
|---|---|---|---|
| F1 | `/health/ready` is anonymous; unhealthy entries return `description = exception.Message`, leaking dependency internals | `src/PriceNegotiationApp.Api/ReadyHealthReport.cs:27` | Strip exception text from the public body (status + duration only) |
| F2 | Login distinguishes `account_locked` from `invalid_credentials` — account enumeration oracle | `src/PriceNegotiationApp.Modules.Identity/Features/Auth/LoginUserHandler.cs:16-29` | Uniform `invalid_credentials` response for unknown user, wrong password, and locked account |
| F3 | Seed credential floor is 8 chars; `.env.example` and README ship `Admin123!`; a deployed demo runs guessable admin creds | `SeedingOptionsValidator.cs:21-29`, `.env.example:4-5`, `README.md:111-112` | Validator requires ≥12 chars with upper+lower+digit; examples use obviously-fake placeholders; compose refuses known-weak values |
| F4 | Rate limiting is fixed-window per raw `RemoteIpAddress`; no forwarded-header story behind a reverse proxy | `WebApplicationBuilderExtensions.cs:88-97` | Document posture in README; keep YAGNI unless a proxy deployment is in scope |
| F5 | `JwtSettings` (Api) duplicates `JwtOptions` (Identity) with weaker validation coverage (no expiry check) | `src/PriceNegotiationApp.Api/Extensions/JwtSettings.cs` vs `JwtOptions.cs` + validators | Single source of truth: Api binds the Identity module's validated options |
| F6 | Authenticated write endpoints have no rate limit beyond auth endpoints | `Login.cs`, `Register.cs` only call `RequireRateLimiting` | Evaluate global partitioned limiter during execution; add only if cheap, else document |

## Fix batches

- **B1 — Info disclosure & enumeration** (F1, F2): health body shape + uniform login
  failures. Regression tests: ready-body shape assertion; login-response uniformity test.
- **B2 — Config foolproofness** (F3, F5): seed password validator hardening, example
  placeholder churn, JWT options de-duplication. Tests: validator unit tests,
  startup-validation integration test.
- **B3 — Documentation & report** (F4, F6 disposition, trade-offs, findings table):
  README security notes, final findings table committed here.

## Verification

- New regression/integration tests per fix where practical (repo already has
  WebApplicationFactory + Testcontainers infrastructure).
- `dotnet list package --vulnerable --include-transitive` clean or triaged with rationale.
- Full suite green: `dotnet test --solution PriceNegotiationApp.slnx`.

## Deliberate trade-offs (documented, not built)

- No refresh tokens / token revocation: access tokens are short-lived, single-audience,
  no sensitive writes beyond demo scope.
- No forwarded-headers middleware: app is deployed directly exposed (compose), not
  behind a proxy.
- Symmetric HMAC JWT signing key: single-service issuance/validation; asymmetric keys
  add key-distribution complexity without a second consumer.
- Registration/login email-enumeration via register-conflict responses: standard UX
  trade-off; login path is being made uniform under B1.
