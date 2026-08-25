# PriceNegotiationApp — Engineering Experience & Operations Backlog

Date: 2026-08-25
Scope: software-development concerns only — observability, CI/CD, panels/dashboards,
developer tooling, repo management files, operational utilities. No business features
(those live in `docs/business-features.md`). Context: portfolio program — every item
should either make the project *easier to run/change/trust* or make the engineering
investment already present **visible**.

---

## 1. Executive Summary

The codebase is disciplined (CPM, analyzers-as-errors, Testcontainers, architecture tests,
MTP) but **under-showcased and under-wired**: telemetry is exported to endpoints that don't
exist locally, cobertura coverage files are generated and then ignored, the SDK is unpinned,
four test projects repeat identical boilerplate, and CI stops at "tests pass" instead of
producing artifacts a reviewer can click.

The backlog below closes those holes. Roughly half of it is small, high-signal work that
makes existing investment legible (dashboards for the OTel/Loki plumbing, a coverage badge,
a one-command local pipeline); the other half hardens supply chain and delivery (SDK pin,
nuget source lockdown, vulnerability gate, image publishing).

---

## 2. Current State Map

| Area | Present | Missing |
|---|---|---|
| Logging | Serilog console + rolling file, request logging, Loki sink package | Log enrichment (actor/route), noise filtering, anywhere for Loki sink to ship to |
| Tracing/Metrics | OpenTelemetry traces + metrics via OTLP env vars | Local collector/dashboard; nothing consumes OTLP in compose |
| Health | `/health/live`, `/health/ready` (DbContext) | Dependency-aware ready details surfaced anywhere |
| CI | format → Release build → tests w/ `--coverage` | Coverage reporting, vuln gate, secret scan, artifact/image publish, badges |
| Tooling files | `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `global.json` (test runner only) | SDK pin, `nuget.config`, shared test conventions, deterministic-build flags |
| Delivery | Dockerfile + compose (api + postgres) | Image publishing, versioning, migration bundle/deploy story |
| Docs | README (excellent), `.http` file, Scalar in dev | CONTRIBUTING/SECURITY/CODEOWNERS, templates, OpenAPI artifact |

---

## 3. Gap Analysis

| # | Gap | Consequence |
|---|---|---|
| E-01 | OTLP/Loki sinks point at nothing in local/compose runs | The strongest part of the stack is invisible during demos |
| E-02 | Coverage collected but never reported/badged | Quality work unverifiable at a glance |
| E-03 | No dependency-vulnerability or secret scanning in CI | Supply-chain risk undetected despite NuGetAudit being on |
| E-04 | SDK version unpinned (`global.json` has test runner only) | Build reproducibility depends on contributor's machine |
| E-05 | 4 test `.csproj`s duplicate OutputType/NoWarn boilerplate | Drift risk; new test projects copy-paste ceremony |
| E-06 | Images not published; no versioning story | "Portfolio" ends at source; no runnable artifact others can pull |
| E-07 | Request logs lack actor/context enrichment; health/scalar spam logs | Logs noisy yet answer fewer questions than they should |
| E-08 | Options validated ad hoc (JWT yes; seeding/db sections inconsistent) | Misconfig surfaces late |
| E-09 | No contribution surface (CODEOWNERS/SECURITY/templates) | Repo reads as solo toy rather than maintained product |

---

## 4. Backlog

Priority: **M** = do first (cheap + high signal), **S** = should, **C** = nice/optional.
Effort: **S** ≤ half day, **M** ≈ a day, **L** = multiple days.

### Theme A — Observability & Panels

#### E-01 · Local telemetry consumer: Aspire Dashboard container · M / S
Standalone `mcr.microsoft.com/dotnet/aspire-dashboard` service added to docker-compose
(development-only profile), wired by setting `OTEL_EXPORTER_OTLP_ENDPOINT` on the api
service. Instant structured logs + traces + metrics UI for the existing instrumentation —
zero application-code changes. This alone makes the OTel work demonstrable.

#### E-02 · Production-shaped observability profile (Grafana + Loki + Tempo) · C / L
Compose profile `observability` provisioning Loki (receiving the existing Serilog Loki sink),
Tempo (OTLP traces), Grafana with pre-provisioned datasource + one committed dashboard JSON:
request rate, p50/p95 latency, error rate by `code`, DB pool saturation, health status.
Kept out of the default profile so `docker compose up` stays minimal.

#### E-03 · Meaningful request logs · M / S
Enrich `UseSerilogRequestLogging` with caller id/roles (post-auth), matched endpoint name,
and negotiation/product route values where present; drop `/health/*`, `/scalar`, and
OpenAPI paths from request logging. One line of config per concern; logs become greppable
per actor instead of per connection.

#### E-04 · Ready-check detail surface · S / S
Extend readiness payload with named checks (identity/catalog/negotiations schemas) so an
unhealthy dependency names itself; pairs naturally with a Grafana health panel in E-02.

### Theme B — CI/CD

#### E-05 · Coverage reporting + badge · M / S
Add ReportGenerator step converting the cobertura files into a consolidated HTML + Cobertura
summary; upload as workflow artifact; feed Codecov (or badge from summary) and add the badge
to README. The data is already produced today — this only makes it visible.

#### E-06 · Vulnerable-package gate · M / S
CI step running `dotnet list package --vulnerable --include-transitive`
(plus `--vulnerable-known-missing` optionally) and failing the build on hits. NuGetAudit
already warns locally; this turns warnings into a merge gate.

#### E-07 · Secret scanning · M / S
Gitleaks GitHub Action on push/PR. Cheap insurance given the repo teaches secrets handling
via user-secrets/.env patterns.

#### E-08 · Publish container images · S / M
On pushes to `develop`/`main`: build multi-stage image, push to GHCR tagged with
branch + short SHA (and semver once E-09 lands). Login-only smoke run against published
image with compose to prove the artifact boots and passes `/health/ready`.

#### E-09 · Versioning · C / M
Tag-driven version stamping (MinVer) surfaced in `/health/live` payload and image tags —
answers "what build am I looking at?" in demos and issues.

### Theme C — Tooling & Management Files

#### E-10 · Pin SDK in `global.json` · M / S
Add `"sdk": { "version": "10.0.x", "rollForward": "latestFeature" }` alongside the existing
test-runner section. Reproducible builds for anyone cloning the portfolio.

#### E-11 · Harden package sources with `nuget.config` · M / S
Explicit single source (nuget.org), clear cached/fallback sources, disable `packages.config`
resolution. Prevents dependency-confusion style surprises and documents supply-chain intent.

#### E-12 · Centralize test-project conventions · M / S
New `Directory.Build.targets` applying, when `IsTestProject`-ish path condition matches
(`$(MSBuildProjectDirectory)` contains `/tests/`): `OutputType=Exe`, common `NoWarn`
(CA1707, S1118), `IsPackable=false`. Shrinks four csprojs to just references — future test
projects get conventions free.

#### E-13 · Deterministic builds + Source Link · S / S
`Directory.Build.props`: `Deterministic=true`, `ContinuousIntegrationBuild` support
(incoming from CI env), Source Link package. PDBs become meaningful; costs minutes.

#### E-14 · One-command local pipeline (`build.ps1`) · M / S
Script: restore → format verify → Debug build → unit tests → integration tests (Docker guard)
→ optional `-Coverage` switch opening the ReportGenerator HTML. New-contributor onboarding
collapses to `.\build.ps1`.

### Theme D — API Surface & Docs

#### E-15 · XML-doc-powered OpenAPI · S / S
Enable `GenerateDocumentationFile` for Api, suppress CS1591 noise wire-in, include XML in
OpenAPI/Scalar so endpoint summaries/description show up in the panel. Portfolio reviewers
read the Scalar page first — make it self-describing.

#### E-16 · OpenAPI artifact in CI · C / S
Publish the generated OpenAPI JSON as a workflow artifact on each Release build;
diff-friendly record of contract evolution.

### Theme E — Operational Utilities

#### E-17 · Migration deployment utility · S / M
Script wrapping `dotnet ef migrations bundle` creation + execution against an arbitrary
connection string; document as the production alternative to startup-applied migrations.
Shows deployment thinking beyond `docker compose up`.

#### E-18 · Uniform configuration validation · S / S
Apply the existing `IValidateOptions` + `ValidateOnStart` pattern (currently JWT-only)
to Database/Seeding/Cors/RateLimiting option objects. Fail-fast with named section errors.

#### E-19 · Data ops runbook + backup profile · C / S
Compose profile adding `pg_dump`/restore sidecar or documented one-liners; runbook in docs/.
Round-trip proof that the volume in compose is real data worth protecting.

### Theme F — Repository Polish

#### E-20 · Community-grade files · M / S
`CONTRIBUTING.md` (short: prerequisites, `.\build.ps1`, conventions), `SECURITY.md`
(reporting + supported versions), `CODEOWNERS`. Signals maintainership quality instantly.

#### E-21 · Issue & PR templates · C / S
Bug/feature issue forms + PR checklist ("docs updated, tests green, format clean").

#### E-22 · README badge row · M / S
CI status + coverage (from E-05) + target framework badges under the title.

---

## 5. Sequencing

| Wave | Items | Rationale |
|---|---|---|
| 1 | E-01, E-03, E-05, E-06, E-07, E-10, E-12, E-22 | Small, independent, immediately visible; most are same-day |
| 2 | E-08, E-11, E-13, E-14, E-15, E-18, E-20 | Hardening + onboarding depth |
| 3 | E-02, E-04, E-09, E-16, E-17, E-19, E-21 | Full-stack observability and delivery maturity |
| Optional | E-02 if effort-constrained, plus future candidates below | Explicitly deferred |

**Future candidates (not scheduled):** Stryker mutation testing on domain modules with a
score gate; Aspire orchestration evaluation (AppHost replacing compose for local F5);
API versioning policy when a v2 becomes real; SBOM (CycloneDX) attached to releases.

---

## 6. Non-Goals

- Kubernetes/Helm manifests — compose + published image is the right ceiling here.
- Feature flags infrastructure, A/B tooling, payment/webhook plumbing — business-side decisions.
- Mono-repo tooling, private feed management, multi-environment IaC — scale the repo doesn't have.
