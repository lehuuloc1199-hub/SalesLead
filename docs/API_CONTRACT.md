# API Contract - SalesLead Platform

## 1. Purpose and Scope

This document defines the external REST API contract for the SalesLead backend service.  
The API supports:
- lead ingestion from upstream channels,
- tenant-scoped lead retrieval for sales users,
- lead activity logging.

Data is persisted in a relational database; API behavior is defined by HTTP semantics, request validation, and authorization rules described below.

## 2. Conventions

- **Base path:** `/api/v1`
- **Content type:** `application/json`
- **Character encoding:** UTF-8
- **Date/time format:** ISO 8601 in UTC (for example: `2026-04-01T09:30:00Z`)
- **Identifiers:** GUID/UUID strings

## 3. Security Model

### 3.1 Ingestion Endpoints
- **Applies to:** `/api/v1/ingest/**`
- **Required header:** `X-Api-Key: <tenant_ingest_api_key>`
- **Failure behavior:** invalid or missing key returns `401 Unauthorized`

### 3.2 Sales Endpoints
- **Applies to:** `/api/v1/tenants/{tenantId}/**`
- **Required header:** `X-User-Id: <sales_user_guid>`
- **Authorization rule:** user must be active and belong to route `tenantId`
- **Failure behavior:**
  - malformed or missing `X-User-Id` -> `401 Unauthorized`
  - user not allowed for tenant -> `403 Forbidden`

## 4. Idempotency and Rate Limiting

- Ingestion endpoints accept optional `Idempotency-Key`.
- A repeated logical request may return:
  - `201 Created` for the first successful write,
  - `200 OK` with `duplicate=true` for duplicate replay.
- On throttling for **single-lead ingest** (`POST /api/v1/ingest/leads`), API returns `429 Too Many Requests` and includes:
  - `Retry-After: <seconds>` header
  - body `{ "retryAfterSeconds": <int> }`
- For **bulk ingest** (`POST /api/v1/ingest/leads/bulk`), the API returns `200 OK` with per-item outcomes; throttled items are reported as `status: "rate_limited"`. When any items are throttled, the response **may** include `Retry-After: <seconds>`.

## 5. Data Contracts

### 5.1 IngestLeadRequest
```json
{
  "firstName": "John",
  "lastName": "Doe",
  "email": "john@example.com",
  "phone": "0123456789",
  "vehicleInterest": "SUV",
  "source": "WebsiteForm",
  "notes": "Wants test drive next week",
  "externalId": "ext-123"
}
```

Validation rules:
- `firstName`: required, max 128
- `lastName`: required, max 128
- `email`: required, valid email, max 256
- `phone`: optional, max 64
- `vehicleInterest`: optional, max 256
- `source`: required, max 64
- `notes`: optional, max 4000
- `externalId`: optional, max 128

### 5.2 CreateActivityRequest
```json
{
  "activityTypeId": "11111111-1111-1111-1111-111111111111",
  "notes": "Called customer, interested in financing",
  "activityDateUtc": "2026-04-01T09:30:00Z"
}
```

Validation rules:
- `activityTypeId`: required GUID
- `notes`: optional, max 2000
- `activityDateUtc`: optional UTC datetime

## 6. Endpoint Definitions

### 6.1 `POST /api/v1/ingest/leads`

Creates one lead from an integration source.

**Headers**
- `X-Api-Key` (required)
- `Idempotency-Key` (optional, recommended)

**Request body**
- `IngestLeadRequest`

**Responses**
- `201 Created`
```json
{
  "leadId": "8ef69af7-b675-4f7f-8bb0-9fcb85f8d9d9",
  "duplicate": false
}
```
- `200 OK` (idempotent replay)
```json
{
  "leadId": "8ef69af7-b675-4f7f-8bb0-9fcb85f8d9d9",
  "duplicate": true
}
```
- `400 Bad Request`
```json
{ "message": "Validation or business rule error" }
```
- `401 Unauthorized`
- `429 Too Many Requests`
```json
{ "retryAfterSeconds": 60 }
```

---

### 6.2 `POST /api/v1/ingest/leads/bulk`

Processes multiple leads in a single request and returns per-item status.

**Headers**
- `X-Api-Key` (required)
- `Idempotency-Key` (optional, recommended)

**Request body**
- `IngestLeadRequest[]` (must contain at least one item)

**Responses**
- `200 OK`
```json
{
  "total": 2,
  "created": 1,
  "duplicate": 1,
  "rateLimited": 0,
  "failed": 0,
  "items": [
    {
      "index": 1,
      "leadId": "8ef69af7-b675-4f7f-8bb0-9fcb85f8d9d9",
      "status": "created",
      "message": null
    },
    {
      "index": 2,
      "leadId": "8ef69af7-b675-4f7f-8bb0-9fcb85f8d9d9",
      "status": "duplicate",
      "message": null
    }
  ]
}
```
- `200 OK` (bulk throttling is per-item)
```json
{
  "total": 3,
  "created": 1,
  "duplicate": 0,
  "rateLimited": 1,
  "failed": 1,
  "items": [
    { "index": 1, "leadId": "8ef69af7-b675-4f7f-8bb0-9fcb85f8d9d9", "status": "created", "message": null },
    { "index": 2, "leadId": null, "status": "rate_limited", "message": "Retry after 60s" },
    { "index": 3, "leadId": null, "status": "failed", "message": "Validation or business rule error" }
  ]
}
```
- `items[].status` values:
  - `created`
  - `duplicate`
  - `rate_limited`
  - `failed`
- `400 Bad Request`
```json
{ "message": "Request body must contain at least one lead." }
```
- `401 Unauthorized`
- `Retry-After: <seconds>` response header may be present when any items are `rate_limited`.

---

### 6.3 `GET /api/v1/tenants/{tenantId}/leads`

Returns a paginated list of leads for a tenant.

**Headers**
- `X-User-Id` (required)

**Query parameters**
- `page` (default: `1`)
- `pageSize` (default: `20`)

**Responses**
- `200 OK`
```json
{
  "items": [
    {
      "id": "8ef69af7-b675-4f7f-8bb0-9fcb85f8d9d9",
      "firstName": "John",
      "lastName": "Doe",
      "email": "john@example.com",
      "source": "WebsiteForm",
      "createdUtc": "2026-04-01T09:00:00Z",
      "lastContactAt": "2026-04-01T10:00:00Z"
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20
}
```
- `401 Unauthorized`
- `403 Forbidden`

---

### 6.4 `GET /api/v1/tenants/{tenantId}/leads/{leadId}`

Returns lead details including activity history.

**Headers**
- `X-User-Id` (required)

**Responses**
- `200 OK`
```json
{
  "id": "8ef69af7-b675-4f7f-8bb0-9fcb85f8d9d9",
  "firstName": "John",
  "lastName": "Doe",
  "email": "john@example.com",
  "phone": "0123456789",
  "vehicleInterest": "SUV",
  "source": "WebsiteForm",
  "notes": "Wants test drive next week",
  "statusName": "New",
  "createdUtc": "2026-04-01T09:00:00Z",
  "updatedUtc": "2026-04-01T10:00:00Z",
  "lastContactAt": "2026-04-01T10:00:00Z",
  "activities": [
    {
      "id": "f89ce484-6281-4f67-a9f1-e4e365f98192",
      "typeName": "Call",
      "notes": "Discussed financing options",
      "activityDate": "2026-04-01T10:00:00Z",
      "createdUtc": "2026-04-01T10:00:01Z"
    }
  ]
}
```
- `401 Unauthorized`
- `403 Forbidden`
- `404 Not Found`

---

### 6.5 `POST /api/v1/tenants/{tenantId}/leads/{leadId}/activities`

Creates a new activity event for a lead.

**Headers**
- `X-User-Id` (required)

**Request body**
- `CreateActivityRequest`

**Responses**
- `201 Created`
```json
{
  "activityId": "f89ce484-6281-4f67-a9f1-e4e365f98192"
}
```
- `400 Bad Request`
```json
{ "message": "Validation or business rule error" }
```
- `401 Unauthorized`
- `403 Forbidden`
- `404 Not Found`

## 7. Error Contract

Unless otherwise specified, error payloads follow:
```json
{
  "message": "Human-readable error description"
}
```

For rate-limited ingestion requests:
```json
{
  "retryAfterSeconds": 60
}
```

## 8. Client Test Harness (cURL)

Set environment variables:

```bash
export BASE_URL="http://localhost:5000"
export TENANT_ID="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
export USER_ID="bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"
export INGEST_API_KEY="your-ingest-api-key"
```

### 8.1 Ingest a single lead
```bash
curl -i -X POST "$BASE_URL/api/v1/ingest/leads" \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: $INGEST_API_KEY" \
  -H "Idempotency-Key: lead-ext-123" \
  -d '{
    "firstName":"John",
    "lastName":"Doe",
    "email":"john@example.com",
    "source":"WebsiteForm",
    "externalId":"ext-123"
  }'
```

### 8.2 Ingest leads in bulk
```bash
curl -i -X POST "$BASE_URL/api/v1/ingest/leads/bulk" \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: $INGEST_API_KEY" \
  -H "Idempotency-Key: bulk-20260401" \
  -d '[
    {"firstName":"A","lastName":"One","email":"a.one@example.com","source":"MetaAds"},
    {"firstName":"B","lastName":"Two","email":"b.two@example.com","source":"GoogleAds"}
  ]'
```

### 8.3 List tenant leads
```bash
curl -i "$BASE_URL/api/v1/tenants/$TENANT_ID/leads?page=1&pageSize=20" \
  -H "X-User-Id: $USER_ID"
```

### 8.4 Get lead details
```bash
curl -i "$BASE_URL/api/v1/tenants/$TENANT_ID/leads/<LEAD_ID>" \
  -H "X-User-Id: $USER_ID"
```

### 8.5 Add lead activity
```bash
curl -i -X POST "$BASE_URL/api/v1/tenants/$TENANT_ID/leads/<LEAD_ID>/activities" \
  -H "Content-Type: application/json" \
  -H "X-User-Id: $USER_ID" \
  -d '{
    "activityTypeId":"11111111-1111-1111-1111-111111111111",
    "notes":"Called and booked showroom visit",
    "activityDateUtc":"2026-04-01T09:30:00Z"
  }'
```
