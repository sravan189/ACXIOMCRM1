# AcxiomCRM — REST API Reference

Base path: `/api`. All endpoints return JSON. Entity Framework entities are **never** exposed — every request/response uses DTOs.

## Authentication (cookie flow)

The API uses the same ASP.NET Core Identity cookie authentication as the MVC UI.

1. **Sign in**: `POST /api/auth/login` with `{ "email", "password" }` in the JSON body.
   The server sets an HttpOnly auth cookie (`Set-Cookie` header).
2. **Call protected endpoints** with the cookie attached (same-origin browser fetch, or an HTTP client with a cookie jar — e.g. `curl -c/-b cookies.txt`).
3. **Sign out**: `POST /api/auth/logout` clears the cookie.

There is no token/JWT flow — keep the cookie.

### API auth behavior notes

- Unauthenticated requests to `/api/*` receive **401 JSON**, never a 302 redirect to the login page (the cookie events handler converts redirects for `/api/*` paths; 403 likewise instead of redirecting to AccessDenied).
- `[IgnoreAntiforgeryToken]` is applied to all API controllers (via `CrmApiBaseController`), so API clients do **not** need anti-forgery tokens. MVC form posts still require them.
- `[ApiController]` is deliberately **not** used: automatic 400s would bypass the standard error envelope below.

## Standard error envelope

Every error response uses this shape:

```json
{
  "error": {
    "code": "validation_failed",
    "message": "One or more validation errors occurred.",
    "details": {
      "Email": ["Enter a valid email address."],
      "general": ["Phone number is already in use."]
    }
  }
}
```

Common `code` values:

| Code | Meaning |
|---|---|
| `validation_failed` | 400 — DTO validation or business-rule violation |
| `not_found` | 404 — record not found (or outside your role scope) |
| `duplicate` | 409 — unique constraint violated (email/phone "already exists") |
| `forbidden` | 403 — authenticated but not allowed (e.g. role scope) |
| `invalid_credentials` | 401 — wrong email/password |
| `account_locked` | 401 — locked out after 5 failed attempts (15 min) |
| `account_disabled` | 401 — account deactivated by an admin |
| `internal_error` | 500 — unexpected error (no stack traces or DB details leaked) |

## Status codes

| Code | Used for |
|---|---|
| 200 OK | successful GET/PUT/POST(login/logout/stage/convert/complete/…) |
| 201 Created | successful POST create (with `Location` header via `CreatedAtRoute`) |
| 204 No Content | successful DELETE |
| 400 Bad Request | validation / business-rule failure |
| 401 Unauthorized | not signed in, bad credentials, locked/disabled account |
| 403 Forbidden | signed in but outside role/record scope |
| 404 Not Found | record missing or not visible to the caller |
| 409 Conflict | duplicate email/phone ("already exists") |
| 429 Too Many Requests | rate limit exceeded (login endpoints) |

## Pagination shape

List endpoints return a paged wrapper (page size clamped to 1–100; default 20):

```json
{
  "data": [ … ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 57,
  "totalPages": 3
}
```

Query params on lists: `page`, `pageSize` (plus per-endpoint filters below).

## Role scoping

All `/api/*` endpoints require authentication (`[Authorize]` on the base controller). Data visibility:

- **SalesExecutive** sees only records where they are the assignee (`AssignedToId == userId`) or creator (`CreatedById == userId`). Sales executives also cannot assign records to other users (the requested assignee is forced to their own id).
- **Manager** and **Admin** see all records.

## Endpoints

### Auth

#### POST /api/auth/login — public, rate-limited
Sign in and receive the auth cookie.

```bash
curl -i -c cookies.txt -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{ "email": "admin@acxiomcrm.local", "password": "Acxiom@123", "rememberMe": false }'
```

Request body (`LoginRequestDto`):

```json
{ "email": "admin@acxiomcrm.local", "password": "Acxiom@123", "rememberMe": false }
```

Success `200`:

```json
{
  "userId": "…",
  "email": "admin@acxiomcrm.local",
  "fullName": "CRM Administrator",
  "roles": ["Admin"],
  "message": "Signed in successfully."
}
```

Errors: `400 validation_failed` (missing/invalid fields), `401 invalid_credentials`, `401 account_locked`, `401 account_disabled`. Every failed login is audit-logged, and 5 consecutive failures trigger a 15-minute lockout (`lockoutOnFailure: true`).

#### POST /api/auth/logout — authenticated
Clears the auth cookie. `200` on success.

---

### Customers — `api/customers`

#### GET /api/customers — authenticated
List/search customers.

Query params: `search` (name/email/phone/company), `status` (`CustomerStatus` enum), `page`, `pageSize`.

```bash
curl -b cookies.txt "https://localhost:5001/api/customers?search=acme&status=Active&page=1&pageSize=20"
```

Returns the paged wrapper of `CustomerDto`.

#### GET /api/customers/{id} — authenticated
`200` + `CustomerDto`, or `404 not_found`.

#### POST /api/customers — authenticated
Create a customer. Returns `201 Created` with `Location` header pointing at the new resource.

Request body (`CustomerCreateDto`):

```json
{
  "name": "Priya Sharma",
  "email": "priya@example.com",
  "phone": "9812345678",
  "companyName": "Acme Pvt Ltd",
  "address": "12 MG Road",
  "city": "Bengaluru",
  "state": "Karnataka",
  "status": "Active",
  "assignedToId": "<user-id>"
}
```

Business rules enforced: email format + uniqueness; phone must satisfy the Indian phone format (`[IndianPhone]`: 10 digits starting 6–9, `+91` prefix and separators accepted) + uniqueness after normalization (`+91 98123 45678` and `9812345678` count as the same number); duplicate customer rejected.

Errors: `400 validation_failed`, `409 duplicate` (email/phone already exists).

#### PUT /api/customers/{id} — authenticated
Full/partial update (`CustomerUpdateDto`, same rules as create). `200` + updated `CustomerDto`; `404 not_found`; `409 duplicate`.

#### DELETE /api/customers/{id} — authenticated
**Soft delete** (deactivates the customer; no hard row deletion). Returns `204 No Content`; `404 not_found`.

---

### Leads — `api/leads`

#### GET /api/leads — authenticated
Query params: `search` (name/company), `status` (`LeadStatus`), `assignedTo`, `page`, `pageSize`. Paged `LeadDto` list.

#### GET /api/leads/{id} — authenticated
`200` + `LeadDto`, or `404`.

#### POST /api/leads — authenticated
Create a lead. New leads always start in status `New` (server-assigned).

Request body (`LeadCreateDto`):

```json
{
  "name": "Rahul Verma",
  "email": "rahul@example.com",
  "phone": "9123456780",
  "companyName": "Globex",
  "source": "Website",
  "expectedValue": 250000,
  "assignedToId": "<user-id>"
}
```

Errors: `400 validation_failed` (bad email/phone, expected value out of range, …), `409 duplicate`.

#### PUT /api/leads/{id} — authenticated
Update lead fields (`LeadUpdateDto`; status is intentionally absent — status changes go through the MVC `POST /Leads/ChangeStatus/{id}` action, which enforces the state machine). `200` / `404` / `409` / `400`.

#### POST /api/leads/{id}/convert — authenticated
Convert a **Qualified** lead: creates a Customer and (optionally) an Opportunity **in one transaction**, then marks the lead `Converted`. Converting a non-Qualified lead fails with `400`. (Lead status transitions other than Convert are handled by the MVC `POST /Leads/ChangeStatus/{id}` action, which enforces the state machine in `LeadService.ChangeStatusAsync` — see [docs/FEATURES.md](FEATURES.md) for the transition map. `Converted` can **only** be reached through Convert.)

Request body (`LeadConvertDto`):

```json
{
  "createOpportunity": true,
  "opportunityName": "Globex — ERP rollout",
  "opportunityAmount": 500000,
  "probability": 60,
  "expectedCloseDate": "2026-12-31"
}
```

Success `200` returns the conversion result (created customer id + optional opportunity id).

---

### Opportunities — `api/opportunities`

#### GET /api/opportunities — authenticated
Query params: `search` (name), `stage` (`OpportunityStage`), `page`, `pageSize`. Paged `OpportunityDto` list.

#### GET /api/opportunities/{id} — authenticated
`200` + `OpportunityDto`, or `404`.

#### POST /api/opportunities — authenticated
Create an opportunity. Returns `201 Created`.

Request body (`OpportunityCreateDto`):

```json
{
  "name": "Acme — Cloud migration",
  "customerId": 3,
  "leadId": null,
  "amount": 750000,
  "stage": "Qualification",
  "probability": 30,
  "expectedCloseDate": "2026-11-30",
  "assignedToId": "<user-id>"
}
```

Business rules: open opportunities require `Amount > 0` (closed ones `≥ 0`); `Probability` must be 0–100; `ExpectedCloseDate` cannot be in the past for an active opportunity. Errors: `400 validation_failed`.

#### PUT /api/opportunities/{id} — authenticated
Update. Same rules. `200` / `404` / `400`.

#### POST /api/opportunities/{id}/stage — authenticated
Move the deal through the stage flow: Qualification → Proposal → Negotiation → Won/Lost, with one-step-back allowed and Won/Lost → Qualification reopen. Illegal moves are rejected with `400 validation_failed`.

```json
{ "stage": "Proposal" }
```

---

### Follow-Ups — `api/followups`

#### GET /api/followups — authenticated
Query params: `filter` (`upcoming` | `overdue` | `completed`), `search`, `assignedTo`, `page`, `pageSize`. Paged `FollowUpDto` list.

#### GET /api/followups/{id} — authenticated
`200` + `FollowUpDto`, or `404`.

#### POST /api/followups — authenticated
Schedule a follow-up. Planned date **cannot be in the past** (`400` if it is). Returns `201 Created`.

```json
{
  "customerId": 3,
  "leadId": null,
  "followUpDate": "2026-10-15T10:30:00",
  "followUpType": "Call",
  "remarks": "Demo the reporting module",
  "assignedToId": "<user-id>"
}
```

#### PUT /api/followups/{id} — authenticated
Edit a planned follow-up. `200` / `404` / `400`.

#### POST /api/followups/{id}/complete — authenticated
Mark completed. `200`.

#### POST /api/followups/{id}/reschedule — authenticated
```json
{ "newDate": "2026-10-20T11:00:00" }
```
New date cannot be in the past. `200` / `400`.

#### POST /api/followups/{id}/cancel — authenticated
Cancel a planned follow-up. `200`.

---

### Reports — `api/reports`

#### GET /api/reports/pipeline — authenticated (data scoped by role)
Per-stage pipeline snapshot:

```json
{
  "stages": [
    { "stage": "Qualification", "count": 5, "totalAmount": 1200000, "weightedSum": 360000 },
    …
  ],
  "totalCount": 12,
  "grandTotalAmount": 5400000,
  "grandWeightedSum": 1890000
}
```

Weighted = amount × probability ÷ 100 (the `WeightedAmount` computed property on `Opportunity`). Sales executives receive the same shape restricted to their own records.

## Rate limiting

The `login` fixed-window policy — **10 attempts per minute per client IP** — is applied to:

- `POST /Account/Login` (MVC)
- `POST /api/auth/login`

Exceeding it returns **429 Too Many Requests**. This is a brute-force control, not a general API throttle (no global rate limit on other endpoints).

## Example session

```bash
BASE=https://localhost:5001

# 1. Sign in
curl -c jar.txt -X POST $BASE/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@acxiomcrm.local","password":"Acxiom@123"}'

# 2. List customers (paged)
curl -b jar.txt "$BASE/api/customers?page=1&pageSize=10"

# 3. Create a customer -> 201 + Location header
curl -i -b jar.txt -X POST $BASE/api/customers \
  -H "Content-Type: application/json" \
  -d '{"name":"Test Co","email":"test@example.com","phone":"9812345678"}'

# 4. Validation failure -> 400 envelope
curl -b jar.txt -X POST $BASE/api/customers \
  -H "Content-Type: application/json" \
  -d '{"name":"Bad","email":"not-an-email","phone":"123"}'

# 5. Sign out
curl -b jar.txt -X POST $BASE/api/auth/logout
```
