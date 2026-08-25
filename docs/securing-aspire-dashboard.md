# Securing the Standalone Aspire Dashboard — Field Notes for AI Agents

Audience: AI agents (and humans) operating this repository's observability overlay.
Everything here was verified empirically against `mcr.microsoft.com/dotnet/aspire-dashboard:9.4`
running via `docker-compose`; doc sources are listed at the bottom. If you are an agent,
read §3 (pitfalls) before touching any of this — every item cost real debugging cycles.

Repo artifacts involved: `compose.observability.yml`, `.env.example`, README
*Local telemetry dashboard* section, and the OTLP gate in
`src/PriceNegotiationApp.Api/Extensions/WebApplicationBuilderExtensions.cs`.

---

## 1. Why bother — what the dashboard exposes

The standalone dashboard renders **request payloads, structured logs, and resource
environment (including secrets)**. Aspire's own security notes flag two distinct attack
surfaces, and our first cut secured only one of them:

| Surface | Endpoint(s) | Risk when open |
|---|---|---|
| Browser UI | `18888` | Anyone with reach sees sensitive telemetry; classic DNS-rebinding target on loopback |
| Telemetry ingestion (OTLP) | gRPC `18889`, HTTP `18890` | **Telemetry spoofing** — an untrusted app can fabricate logs/traces claiming your API's `service.name`, poisoning what operators see; also resource-exhaustion spam |

A UI banner literally warns *"telemetry endpoint is unsecured"* until ingestion is locked.
Silencing the banner by other means does not fix anything.

## 2. The fully-secured configuration

Standalone-mode defaults are deliberately mixed: frontend = browser-token (secure),
ingestion = unsecured. Full hardening = keep the first, add the second:

```yaml
services:
  api:
    environment:
      OTEL_EXPORTER_OTLP_ENDPOINT: http://aspire-dashboard:18889
      # NOTE: key=value form here — this is OpenTelemetry's env-var syntax.
      OTEL_EXPORTER_OTLP_HEADERS: x-otlp-api-key=${ASPIRE_OTLP_API_KEY}

  aspire-dashboard:
    image: mcr.microsoft.com/dotnet/aspire-dashboard:9.4
    environment:
      DASHBOARD__FRONTEND__AUTHMODE: BrowserToken
      DASHBOARD__FRONTEND__BROWSERTOKEN: ${ASPIRE_DASHBOARD_TOKEN:?set ASPIRE_DASHBOARD_TOKEN}
      DASHBOARD__OTLP__AUTHMODE: ApiKey
      DASHBOARD__OTLP__PRIMARYAPIKEY: ${ASPIRE_OTLP_API_KEY:?set ASPIRE_OTLP_API_KEY}
    ports:
      - "127.0.0.1:18888:18888"   # UI loopback-only; never publish OTLP ports
```

Rules of thumb:

- Config keys use double-underscore env mapping: `Dashboard:Otlp:PrimaryApiKey`
  → `DASHBOARD__OTLP__PRIMARYAPIKEY`.
- `${VAR:?message}` makes compose refuse to start without secrets — same fail-fast
  pattern as `JWT__SECRETKEY` in the base file.
- Bind the UI to `127.0.0.1`; never publish `18889/18890` to the host.
- Login URL shape: `http://127.0.0.1:18888/login?t=<ASPIRE_DASHBOARD_TOKEN>`.
- Avoid the shortcut `DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true` — it flips
  *frontend, OTLP, and MCP* to anonymous in one shot.
- Sender header goes through `OTEL_EXPORTER_OTLP_HEADERS` in `key=value` form
  (`x-otlp-api-key=<key>`); multiple headers separate with commas.

## 3. Pitfalls encountered while getting there (agent-tested)

### P1 — Two different "header assignment" syntaxes
OpenTelemetry's env var wants `x-otlp-api-key=SECRET` (`=`). A raw `curl -H` wants
`x-otlp-api-key: SECRET` (`:`). Mixing them up produced a 401-with-correct-key mystery:
curl silently dropped the malformed header, the dashboard logged
*"API key from 'x-otlp-api-key' header is missing"*, and nothing else hinted why.
**Lesson:** when auth fails, dump what was actually sent (`curl -v`) and read the
server-side rejection reason in logs — the dashboard logs the precise cause at info level.

### P2 — You cannot verify gRPC ingestion with naive curl
Port `18889` speaks gRPC: plain HTTP/1.1 POSTs return misleading `400`s regardless of auth
state. A correct probe needs `--http2-prior-knowledge`, `content-type: application/grpc`,
a `te: trailers` header, and unary framing (`00 00 00 00 00` for an empty
`ExportTraceServiceRequest`). **Shortcut:** probe the OTLP/**HTTP** port `18890` first —
auth failures surface cleanly as `401` without any framing ceremony.

### P3 — Expected status codes differ per probe type
With ApiKey enabled (verified matrix):

| Probe | Result |
|---|---|
| OTLP/HTTP `18890`, no key | `401` |
| OTLP/HTTP `18890`, wrong key | `401` |
| OTLP/gRPC `18889`, valid key + empty message | `HTTP/2 200`, `grpc-status: 0` |

An empty-but-valid protobuf body is fine for auth verification; don't chase `400`s about
payload validity — that proves auth already passed.

### P4 — pwsh reserved variables create false-positive checks
`$home = curl … ; "banner-gone=$(-not ($html -match 'unsecured'))"` printed `True`
against an **empty string** because `$HOME` is read-only in pwsh. Always assert a
positive signal too (page bytes, `<title>` presence) alongside absence checks.

### P5 — Pipeline truncation corrupts exit codes
`dotnet build 2>&1 | Select-Object -First 3 && git commit …` once committed a broken
build: `-First 3` closes the pipe early, killing dotnet mid-write, and the pipeline's
success came from the last cmdlet. Pattern that behaves:

```powershell
$out = dotnet build 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) { $out | Select-String 'error' } else { <next step> }
```

Related trap: running `dotnet test --no-build` right after editing test code validates the
previous binary. Rebuild first, always.

### P6 — Container build ≠ local build: copy analyzer/config context
The Dockerfile originally copied only `Directory.Build.props/Packages.props`. Result:
`.editorconfig` was missing in-image, Meziantou rule MA0048 (one-type-per-file) fired as
an **error only inside docker build**, while local builds were clean. When a project leans
on `.editorconfig`/`Directory.*` for severity shaping, the image context must include
every file those mechanisms read.

### P7 — Plural `TargetFrameworks` forces explicit publish framework
`<TargetFrameworks>net10.0</TargetFrameworks>` (single value, plural tag) makes
`dotnet publish -c Release` fail with NETSDK1129 *inside the container*, although plain
`build` works everywhere. Fix: `-f net10.0` on the publish line.

### P8 — Don't assume tools exist in runtime images
`aspnet:10.0` ships **neither wget nor curl** — a `HEALTHCHECK CMD wget …` yields a
permanently `unhealthy` container (only `dotnet` is guaranteed). Either drop the probe
(external systems can call `/health/*`) or install a fetcher explicitly.

### P9 — Compose interpolation is your fail-fast friend
`${VAR:?set VAR}` in the overlay reproduces the repo's existing secret handling and turns
"dashboard starts wide open because someone forgot the token" into "compose refuses to
start". Validate both paths: `config --quiet` with the variable set, and confirm the
error message when unset.

### P10 — SPA routing confuses naive auth checks
`GET /` returns `302 → /structuredlogs` whether you're authenticated or not (anonymous
gets bounced again to `/login`). Verify auth by following redirects with the cookie jar
and asserting the final `200` + non-trivial body size — not by judging the first hop.

## 4. Verification playbook (reusable)

1. **Config gate:** `docker compose -f … -f … config --quiet` — must pass with secrets
   set and fail loudly without them.
2. **UI gate:** anonymous `GET /login?t=<bad>` vs token login → expect `Set-Cookie:
   .Aspire.Dashboard.Auth=…` and a followed `200` on `/structuredlogs`.
3. **Ingestion gates:** from a throwaway `curlimages/curl` container attached to the
   compose network, POST to `:18890/v1/traces`: no key → `401`; wrong key → `401`;
   sender configured exactly like the api service → exporter stays silent in logs while
   traffic generates traces (visible in the UI).
4. **End-to-end sanity:** register/login through the API, then eyeball the trace in the UI
   (in-memory only — restarting the dashboard wipes telemetry by design).

## 5. Deliberately not done

- HTTPS transport on loopback (requires dev-cert trust inside containers; plaintext
  loopback + tokens accepted as local-dev trade-off, matching Aspire's own guidance).
- Host filtering allow-list — only becomes relevant if anonymous access is ever enabled.
- Persistent telemetry storage — the dashboard is a diagnostic window, not a sink.

## Sources

- aspire.dev/dashboard/configuration (frontend/OTLP/API auth reference)
- aspire.dev/dashboard/security-considerations (spoofing + resource-exhaustion threats)
- hub.docker.com/r/microsoft/dotnet-aspire-dashboard (env-var table for the image)
- Live probe matrix run against `aspire-dashboard:9.4` on this repo's compose network
