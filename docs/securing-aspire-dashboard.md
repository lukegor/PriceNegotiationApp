# Securing the Standalone Aspire Dashboard — Playbook & Field Notes for AI Agents

> **Portability:** This document is repository-agnostic. Copy it (or its checklist) next to
> any `docker-compose` setup that runs `mcr.microsoft.com/dotnet/aspire-dashboard` in
> standalone mode. Everything was verified live against image tag `9.4`; treat behavior
> claims as version-pinned and re-verify after major upgrades.
>
> **If you are an AI agent:** read §6 (Pitfalls) *before* writing any command. Every item
> there produced a real, wasted debugging cycle during a single hardening session.

Sources: `aspire.dev/dashboard/configuration`, `aspire.dev/dashboard/security-considerations`,
Docker Hub env-var table for `microsoft/dotnet-aspire-dashboard`.

---

## 1. TL;DR agent checklist

1. Frontend (`18888`) → `BrowserToken` with a pinned token; bind UI port to `127.0.0.1`.
2. Ingestion (`18889` gRPC, `18890` HTTP) → `ApiKey` auth; sender adds header via
   `OTEL_EXPORTER_OTLP_HEADERS=x-otlp-api-key=<key>`.
3. Never publish OTLP ports to the host; never use
   `DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true` outside throwaway sandboxes.
4. Make both secrets required in compose (`${VAR:?message}`) so the stack cannot start
   half-configured.
5. Verify with four gates (§5): config interpolation, UI login, ingestion negative probes
   (`401`), ingestion positive probe (`200`/`grpc-status: 0`).

## 2. Threat model — why "it's just localhost" is not enough

| Surface | Endpoint(s) | Risk when open |
|---|---|---|
| Browser UI | `18888` | Sensitive payloads/logs/secrets visible; loopback services remain reachable by any local process; DNS-rebinding can reach unauthenticated localhost ports |
| Ingestion (OTLP) | `18889` (gRPC), `18890` (HTTP) | **Telemetry spoofing** — fabricated logs/traces claiming a trusted `service.name`; resource-exhaustion spam (bandwidth/CPU/memory spent decoding junk even if later evicted) |

Standalone-mode defaults are intentionally mixed: frontend = `BrowserToken` (secure),
ingestion = unsecured. The UI shows a persistent warning until ingestion is secured.
Silencing that warning without enabling API-key auth hides the problem instead of fixing it.

## 3. Reference configuration (overlay pattern)

```yaml
# docker-compose.observability.yml (example name)
services:
  <your-app>:
    environment:
      OTEL_EXPORTER_OTLP_ENDPOINT: http://aspire-dashboard:18889
      # key=value form — this is OpenTelemetry env-var syntax, NOT an HTTP header
      OTEL_EXPORTER_OTLP_HEADERS: x-otlp-api-key=${ASPIRE_OTLP_API_KEY}

  aspire-dashboard:
    image: mcr.microsoft.com/dotnet/aspire-dashboard:9.4   # pin minor; tags move fast
    environment:
      DASHBOARD__FRONTEND__AUTHMODE: BrowserToken
      DASHBOARD__FRONTEND__BROWSERTOKEN: ${ASPIRE_DASHBOARD_TOKEN:?set ASPIRE_DASHBOARD_TOKEN}
      DASHBOARD__OTLP__AUTHMODE: ApiKey
      DASHBOARD__OTLP__PRIMARYAPIKEY: ${ASPIRE_OTLP_API_KEY:?set ASPIRE_OTLP_API_KEY}
    ports:
      - "127.0.0.1:18888:18888"
```

Rules of thumb:

- Config keys map with double underscore: `Dashboard:Otlp:PrimaryApiKey`
  → `DASHBOARD__OTLP__PRIMARYAPIKEY`.
- `${VAR:?message}` = fail-fast when secrets are missing (mirrors how you should already
  treat DB passwords/JWT keys).
- UI login URL shape: `http://127.0.0.1:18888/login?t=<ASPIRE_DASHBOARD_TOKEN>`.
- Sender headers go through `OTEL_EXPORTER_OTLP_HEADERS` as comma-separated `name=value`
  pairs. Multiple senders share the same key unless you issue per-sender keys.
- Avoid `DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true` — it flips frontend, OTLP,
  *and* MCP endpoints to anonymous simultaneously.
- The dashboard persists telemetry **in memory only** — restarting wipes it. It is a
  diagnostic window, not a sink.

## 4. Environment variable reference (verified on 9.4)

| Variable | Purpose |
|---|---|
| `ASPNETCORE_URLS` | Frontend bind address (default `http://+:18888`) |
| `DOTNET_DASHBOARD_OTLP_ENDPOINT_URL` | OTLP/gRPC listen URL (default `http://+:18889`) |
| `DOTNET_DASHBOARD_OTLP_HTTP_ENDPOINT_URL` | OTLP/HTTP listen URL (default `http://+:18890`) |
| `DASHBOARD__FRONTEND__AUTHMODE` | `BrowserToken` (default) / `OpenIdConnect` / `Unsecured` |
| `DASHBOARD__FRONTEND__BROWSERTOKEN` | Pin the browser token (else generated per launch, printed to logs) |
| `DASHBOARD__OTLP__AUTHMODE` | `Unsecured` (standalone default!) / `ApiKey` / `Certificate` |
| `DASHBOARD__OTLP__PRIMARYAPIKEY` | The API key senders must present |
| `DASHBOARD__API__DISABLED` | Telemetry HTTP API off by default — leave off unless needed |

## 5. Verification playbook

Run all four gates after any change. Gates 3–4 are the ones agents typically skip.

**Gate 1 — config interpolation**

```bash
docker compose -f docker-compose.yml -f compose.observability.yml config --quiet   # must pass
unset ASPIRE_DASHBOARD_TOKEN ASPIRE_OTLP_API_KEY
docker compose -f … config --quiet                                                  # must FAIL loudly
```

**Gate 2 — UI auth**

```bash
curl -s -i "http://127.0.0.1:18888/login?t=<token>"        # expect Set-Cookie + 302
curl -s -b cookies.txt -L -o page.html -w "%{http_code}" \
     --compressed http://127.0.0.1:18888/                   # expect 200, non-trivial body
```

**Gate 3 — ingestion rejects untrusted senders** (from a throwaway container on the same
compose network):

```bash
docker run --rm --network <project>_default curlimages/curl:latest \
  -s -o /dev/null -w "%{http_code}" -X POST \
  http://aspire-dashboard:18890/v1/traces \
  -H "Content-Type: application/x-protobuf"
# expect 401; wrong key also 401
```

**Gate 4 — trusted sender succeeds**
Either run your real app and confirm zero exporter errors in its logs, or probe gRPC
directly: `--http2-prior-knowledge`, `content-type: application/grpc`, `te: trailers`,
body = unary-framed empty message (bytes `00 00 00 00 00`), plus the key header →
expect `HTTP/2 200` and `grpc-status: 0`. See §6 P1/P2 before improvising here.

## 6. Pitfalls catalogue (each one bit a real agent)

Format: **Symptom → Root cause → Rule.**

### P1 — Two header-assignment syntaxes get confused
Symptom: `401` with the correct key; server logs *"API key from 'x-otlp-api-key' header is
missing"* while you swear you sent it.
Root cause: OpenTelemetry env vars use `name=value` (`OTEL_EXPORTER_OTLP_HEADERS=x-otlp-api-key=k`),
raw HTTP headers use `name: value` (`curl -H "x-otlp-api-key: k"`). An `-H k=v` form makes
curl silently drop the header.
Rule: dump what was actually sent (`curl -v`) and read the server's rejection log — the
dashboard states the precise missing-header reason at info level.

### P2 — Naive curl cannot probe the gRPC port
Symptom: `400` on `:18889/v1/traces` regardless of auth state.
Root cause: gRPC needs HTTP/2 prior knowledge, grpc content-type, `te: trailers`, and
unary length-prefix framing (`00` + uint32 BE length + message).
Rule: verify auth against the OTLP/**HTTP** port (`18890`) first; reserve framed gRPC
probes for final confirmation.

### P3 — Status-code expectations differ per probe type
With ApiKey enabled: no key → `401`; wrong key → `401`; valid key + empty protobuf body →
auth passes (payload errors surface afterwards as `400`). Don't interpret a payload `400`
as auth failure, and don't chase body validity when testing auth.

### P4 — Reserved shell variables create false-positive assertions
pwsh example: assigning `$home` silently fails (read-only), downstream "negative" checks
run against empty strings and report success.
Rule: never reuse automatic variable names; pair every absence-assertion with a
presence-assertion (e.g., page bytes > N AND title matches).

### P5 — Truncated pipelines corrupt exit codes
`dotnet build 2>&1 | Select-Object -First 3 && git commit` committed broken code: `-First`
closes the pipe early, killing dotnet mid-write; the pipeline's success came from the last
cmdlet.
Rule: capture full output (`| Out-String`), then branch strictly on `$LASTEXITCODE`.
Related: never validate with `--no-build` immediately after editing sources.

### P6 — Container build ≠ host build: copy analyzer/config context
Symptom: analyzer fires as error only inside `docker build` (e.g., MA0048), clean locally.
Root cause: `.editorconfig` shapes severities; the build context copied source but not the
config file.
Rule: the image context must include every file analyzers/SDK mechanisms read —
`.editorconfig`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`,
`nuget.config` if present.

### P7 — Plural `TargetFrameworks` forces explicit publish framework
`<TargetFrameworks>net10.0</TargetFrameworks>` (single value, plural tag) +
`dotnet publish` without `-f` ⇒ NETSDK1129 inside containers, while plain `build` works
on host and CI.
Rule: add `-f <tfm>` to container publish steps whenever the props file uses the plural
tag — or switch the props to singular `TargetFramework`.

### P8 — Runtime images ship almost no tools
`aspnet:*` contains neither wget nor curl (only dotnet). A `HEALTHCHECK CMD wget …`
produces a permanently `unhealthy` container whose logs say `exec: no such file`.
Rule: remove dead probes (prefer external `/health` checks) or install tooling explicitly
and accept the size cost.

### P9 — Compose interpolation is your admission control
`${TOKEN:?set TOKEN}` converts "stack starts wide open because a secret was forgotten"
into "compose refuses to start". Validate both directions: pass with variables set, fail
loudly without them.

### P10 — SPA routing breaks naive auth checks
Authenticated `GET /` may still return `302 → /structuredlogs`; anonymous requests bounce
elsewhere. Judging the first hop misleads.
Rule: follow redirects with the cookie jar and assert the final status **and** non-trivial
body; combine with positive signals (title/content markers), never absence alone.

### P11 — App-side gating belongs in code, not just compose
Exporters retry loudly against dead endpoints. Gate telemetry registration on endpoint
presence (e.g., register `UseOtlpExporter()` only when `OTEL_EXPORTER_OTLP_ENDPOINT` is
set). Base compose stays clean, overlays opt in, and environments without collectors stay
silent.

## 7. Adoption checklist for a new repository

- [ ] Overlay file created; base compose untouched; dashboard UI bound to `127.0.0.1`.
- [ ] Image tag pinned to a minor version; upgrade = deliberate edit.
- [ ] Both secrets required via `:?` interpolation; documented in `.env.example`.
- [ ] App exports only when `OTEL_EXPORTER_OTLP_ENDPOINT` is configured.
- [ ] Sender header wired through `OTEL_EXPORTER_OTLP_HEADERS`.
- [ ] Four verification gates executed and recorded.
- [ ] README documents: up/down commands, login URL shape, seed/token locations,
      reproduction steps for failures.

## 8. Deliberately out of scope

HTTPS transport on loopback (dev-cert trust inside containers outweighs benefit locally);
host-filtering allow-lists (only relevant if anonymous access is ever enabled); persistent
telemetry storage; per-sender API keys beyond one trusted deployment.
