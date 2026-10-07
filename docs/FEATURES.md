# AcxiomCRM — Feature Reference

Complete module-by-module feature list. Role visibility: Admin and Manager see all CRM records; SalesExecutive sees only records assigned to them or created by them (see [docs/SECURITY.md](SECURITY.md)).

## Authentication & user management

- **Register / Login / Logout** — Identity-backed forms with client + server validation; lockout after 5 failed attempts (15 min); login rate-limited to 10/min/IP.
- **User administration** (Admin only) — list/search users, create, edit, activate/deactivate (`ToggleActive`), reset password, unlock locked-out accounts.
- **Role administration** (Admin only) — list/create/delete roles, assign roles to users (role changes are audit-logged).

## Dashboard (default landing page)

- **8 KPI cards**: Total Customers, Total Leads, Open Leads, Total Opportunities, Open Opportunities, Won Opportunities, Lost Opportunities, Total Pipeline Value (sum of eligible open opportunity amounts). All values respect the viewer's role scope.
- **3 Chart.js charts**: Lead Status distribution (New/Contacted/Qualified/Lost/Converted), Opportunity Pipeline by stage (Qualification/Proposal/Negotiation/Won/Lost), Monthly Sales totals.
- **Date-range filters** (`from`/`to`) recompute KPIs and charts for the selected period.

## Customers

- **CRUD** — create, view, edit, and deactivate (soft-delete: sets status inactive; no hard row deletion).
- **Validation** — name required (max length); email required, valid format, **unique**; phone required, Indian format (`[IndianPhone]` — 10 digits starting 6–9), **unique** with normalization (`+91 98123 45678` ≡ `9812345678`); duplicates rejected with 409 on the API.
- **Search/filter** — by name, email, phone, company; status filter; sorting; pagination.
- **Assignment** — customers can be assigned to a sales user; sales executives can only keep/see their own.

## Leads

- **CRUD** — name (mandatory), email/phone (common validation rules), company, source, expected value (configured numeric range), status, assignment.
- **Status state machine** (enforced server-side in `LeadStatusTransitions`):
  - `New` → Contacted / Unqualified / Lost
  - `Contacted` → Qualified / Unqualified / Lost
  - `Qualified` → Unqualified / Lost (→ Converted **only** via Convert)
  - `Unqualified` → New / Contacted / Lost
  - `Converted`, `Lost` — terminal
- **Convert** (`POST /Leads/Convert/{id}`, `POST /api/leads/{id}/convert`) — converts a **Qualified** lead into a Customer (new `CUST-` code) plus an optional linked Opportunity, **in a single transaction**, then marks the lead `Converted`. Audited as `"Lead Converted"`.
- **Search/filter** — by name, company, status, assigned user; sorting; pagination.

## Opportunities

- **CRUD** — name, linked customer/lead, amount, stage, probability, expected close date, assignment.
- **Business rules** (server-enforced): open opportunities require `Amount > 0` (closed ≥ 0); `Probability` 0–100; `ExpectedCloseDate` cannot be in the past for an active opportunity.
- **Stage flow** (enforced in `OpportunityService`): Qualification → Proposal → Negotiation → Won / Lost, with **one-step-back** allowed (e.g. Proposal → Qualification) and **reopen** of Won/Lost back to Qualification.
- **Kanban Pipeline view** (`/Opportunities/Pipeline`) — deals grouped by stage with per-stage totals and weighted sums (amount × probability ÷ 100).
- **Search/filter** — by name, customer, stage, status; sorting; pagination.

## Follow-Ups

- **Tab-based list** — `upcoming` / `overdue` / `completed` tabs (default: upcoming), with search, assignee filter, and pagination.
- **Schedule** — link to a customer and/or lead; type (Call/Email/Visit/…), planned date/time, remarks, assignee. **Planned date cannot be in the past** for a new/planned follow-up.
- **Actions** — **Complete**, **Reschedule** (new date also cannot be in the past), **Cancel**. All state changes are audit-logged.
- **Filters** — by date, status, assigned user, related customer/lead.

## Activities

- **CRUD** — log activities against a customer or lead: type, subject, description, date, assignee, status.
- **Search/filter** — by activity type, date, status, assigned user; pagination.

## Reports (8)

Every report supports filters, column sorting, pagination, role-scoped data, and CSV export via `?format=csv` (export pulls up to 5,000 rows; views and exports are audit-logged):

| Report | Key data | Access |
|---|---|---|
| Customer Report | customer, status, owner, created date | Admin / Manager / authorized user |
| Lead Report | source, status, owner, conversion | Admin / Manager / sales scope |
| Follow-Up Report | date, status, owner, overdue activities | all authorized roles |
| Opportunity Report | stage, amount, probability, expected close date | all authorized roles |
| Pipeline Report | stage-wise, owner-wise, weighted pipeline | Admin / Manager; scoped sales view |
| Conversion Report | leads converted vs not converted, conversion rate | Admin / Manager; scoped sales view |
| User Activity Report | activity/action counts by user | Admin / Manager where permitted |
| Audit Log Report | security and business events (user, action, entity, record, old/new values, IP, date) | **Admin only** |

## REST API

Full reference in [docs/API.md](API.md). Highlights:

- Cookie-auth flow (`POST /api/auth/login` → auth cookie → protected endpoints → `POST /api/auth/logout`).
- Endpoints: **auth** (login/logout), **customers** (full CRUD + soft delete), **leads** (CRUD + convert), **opportunities** (CRUD + stage change), **follow-ups** (CRUD + complete/reschedule/cancel), **reports** (pipeline snapshot).
- Standard error envelope `{error:{code,message,details}}`; status codes 200/201/204/400/401/403/404/409/429; paged list shape `{data,page,pageSize,totalCount,totalPages}`; DTOs only — EF entities never serialized.
- `/api/*` auth failures return JSON 401/403 instead of HTML redirects.

## Audit logging

Every mutation (create/update/delete/deactivate/convert/stage change/complete/reschedule/cancel), every login/logout/lockout, role changes, password admin actions, and every report view/export writes an `AuditLog` row (who, what action, which entity/record, old → new values, when, from which IP). Viewable in the Admin-only Audit Log report.

## Cross-cutting UX

- Responsive Bootstrap layout, consistent navigation across modules.
- Inline validation messages next to fields (unobtrusive client validation) + server-side enforcement.
- Success/error/warning notifications; confirmation dialogs on destructive actions.
- Friendly error pages for 400/401/403/404/500 (MVC) and the JSON error envelope (API).
