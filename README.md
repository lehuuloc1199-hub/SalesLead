# Sales Lead Management Tool — Backend

Modular monolith (.NET 8) aligned with `docs/SYSTEM_DESIGN.md`: **multi-tenant**, **microservice-first data ownership** (single SQLite file in Phase 1), **ingest** + **sales** APIs, **transactional outbox** (dispatcher logs events), **subscription-based in-memory rate limiting** (no Redis/Kafka).

## Documentation

- System design: `docs/SYSTEM_DESIGN.md`
- API contract: `docs/API_CONTRACT.md`
- Note: this repository implements **Phase 1** in `docs/SYSTEM_DESIGN.md` (single API process + SQLite, no Redis/Kafka broker runtime).

## Video Submission (Code Challenge)

Video walkthrough (5-10 minutes):  
[LocLe_Keyloop_ScenarioC_SalesLead_Walkthrough.mp4](https://drive.google.com/file/d/1aN1phPOoudfwcMb5__XcQY4xsuE6yLb-/view)

The video covers:

- A brief introduction to myself and the chosen scenario.
- A walkthrough of system design and implementation highlights.
- A summary of the AI collaboration story (1-2 minutes).
- A brief demonstration of the application.
- Key learnings and challenges faced.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Build & run

**Windows one-click demo:** double-click `run-demo.bat` in the `SalesLead` folder (restores, builds, starts the API on **localhost**—prefers port **5055**, picks the next free port if that one is in use—then opens Swagger when healthy).

```bash
cd SalesLead
dotnet restore
dotnet build
dotnet run --project src/SalesLead.Api
```

- Swagger UI: `http://localhost:5055/swagger` (see `launchSettings.json` for ports).
- SQLite file: `src/SalesLead.Api/Data/saleslead.db` (created on first run).

## Tests

```bash
dotnet test
```

Integration tests use environment `IntegrationTest` and a temporary SQLite file (see `Program.cs`).

## EF Core migrations

Local tool (manifest in `.config/dotnet-tools.json`):

```bash
dotnet tool restore
dotnet tool run dotnet-ef migrations add <Name> --project src/SalesLead.Infrastructure --startup-project src/SalesLead.Api --output-dir Data/Migrations
```

## Demo credentials (seed data)

| Item | Value |
|------|--------|
| **Tenant A** (Essential plan) | Id `11111111-1111-1111-1111-111111111111` |
| **Tenant B** (Professional plan) | Id `22222222-2222-2222-2222-222222222222` |
| **Ingest API key A** | `sk_demo_tenant_a_essential` |
| **Ingest API key B** | `sk_demo_tenant_b_professional` |
| **Sales user A** | Header `X-User-Id`: `aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa` |
| **Sales user B** | Header `X-User-Id`: `bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb` |

### Ingest lead

`POST /api/v1/ingest/leads`  
Headers: `X-Api-Key: <key>`, optional `Idempotency-Key: <unique>`  
Body (JSON): `firstName`, `lastName`, `email`, `source`, optional `phone`, `vehicleInterest`, `notes`, `externalId`.

### Ingest leads (bulk)

`POST /api/v1/ingest/leads/bulk`  
Headers: `X-Api-Key: <key>`, optional `Idempotency-Key: <base>`  
Body (JSON): array of ingest objects (same schema as single ingest).  
Behavior: returns `200 OK` with per-item status (`created`, `duplicate`, `rate_limited`, `failed`) and includes `Retry-After` when any item is rate-limited.

### Sales APIs

All require header **`X-User-Id`** matching a user in the **route tenant**.

- `GET /api/v1/tenants/{tenantId}/leads?page=1&pageSize=20`
- `GET /api/v1/tenants/{tenantId}/leads/{leadId}`
- `POST /api/v1/tenants/{tenantId}/leads/{leadId}/activities` — body: `activityTypeId`, optional `notes`, `activityDateUtc`.

Use Swagger to discover `activityTypeId` values after seed (or query DB).

### Health

- `GET /health/live`
- `GET /health/ready`

## Solution layout (bounded contexts)

| Area | Location |
|------|----------|
| **Tenant & Access** (entities) | `src/SalesLead.Infrastructure/Entities` — `Tenant`, `TenantApiKey`, `SubscriptionPlan`, `TenantSubscription`, `TenantUsageDaily` |
| **Lead Ingestion** | `src/SalesLead.Api/Services/LeadIngestionService.cs`, `src/SalesLead.Api/Controllers/IngestController.cs`, `src/SalesLead.Infrastructure/Entities/LeadIngestionRecord.cs` |
| **Lead Core** | `src/SalesLead.Api/Services/LeadSalesService.cs`, `src/SalesLead.Api/Controllers/LeadsController.cs`, `src/SalesLead.Infrastructure/Entities` (`Lead`, `LeadActivity`, `OutboxMessage`, lookups) |
| **Cross-cutting middleware/hosting** | `src/SalesLead.Api/Middleware` (correlation + auth), `src/SalesLead.Api/Hosting/OutboxDispatcherHostedService.cs` |

## AI Collaboration Narrative

I used a generative AI assistant (e.g. Cursor) as a **design and implementation sparring partner**, not as an unreviewed source of truth. The goal was a **clear, auditable** result: explicit tenancy rules, a data model that can be split along service boundaries later, and a Phase 1 codebase that builds, tests, and runs without extra infrastructure.

### High-level strategy for guiding the AI

- **Frame the problem as a real SaaS backend** before asking for code: multi-tenant isolation, ingestion vs. sales workloads, idempotency, and fair use under load. I steered the assistant toward **explicit trade-offs** (shared schema vs. dedicated schema vs. dedicated database; monolith vs. future service boundaries) rather than accepting a single “happy path” sketch.
- **Separate design from Phase 1 delivery**: the assistant helped structure `docs/SYSTEM_DESIGN.md` around logical service ownership, traffic split, package-based limits (Essential vs. Professional as **configuration** on subscription plans, not hard-coded magic numbers), and how to mitigate bottlenecks.
- **Constrain scope to verifiable Phase 1**: one ASP.NET Core API, one SQLite database, middleware-based auth headers, transactional outbox with an in-process dispatcher, and tests that assert **tenant isolation** and core flows. I asked the model to align endpoints and behavior with `docs/API_CONTRACT.md` once that contract existed.

### How I verified and refined the output

- **Build and test loop**: after each meaningful change, `dotnet build` and `dotnet test`. Failures drove targeted follow-up prompts or hand-edits rather than broad regenerations.
- **Runtime checks**: exercised **Swagger** and manual requests against seeded tenants to confirm headers (`X-Api-Key`, `X-User-Id`) and idempotency behavior.
- **Consistency pass**: compared controller and service behavior against `docs/API_CONTRACT.md` and `docs/SYSTEM_DESIGN.md`; removed or rewrote suggestions that over-engineered Phase 2 (Redis, brokers) into mandatory runtime dependencies.

### How I ensured final quality

- **Security and tenancy**: ingest resolves tenant from the API key; sales routes require a user that belongs to the **route** tenant. Duplicates are handled with idempotency and optional external identifiers where applicable.
- **Data model discipline**: tables and modules mirror **microservice-first ownership** while Phase 1 uses one physical database for operational simplicity; cross-cutting rules (outbox, usage counters) stay coherent with the written design.
- **Operational realism without scope creep**: rate limiting is **per-tenant**, driven from subscription plan data.

Details of isolation models, example plan limits, and Phase 2 topology are intentionally documented in **`docs/SYSTEM_DESIGN.md`** rather than duplicated here.

## License

Use and distribution are subject to the terms supplied with the exercise requirements.
