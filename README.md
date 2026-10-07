# AcxiomCRM

A complete, role-based Customer Relationship Management (CRM) web application built with **ASP.NET Core 8 MVC** and **Entity Framework Core**, covering the full customer–sales lifecycle: lead capture → follow-ups → opportunities → reporting, with authentication, role-based authorization, audit logging, validation (client + server + business rules), and a secured REST API.

This project was implemented strictly against the 19-page project specification (`AcxiomCRM_Complete_Project_Assignment.pdf`), including its Final Acceptance Scenario (§17.19).

## Features at a glance

- **Authentication & roles** — ASP.NET Core Identity with three roles: `Admin`, `Manager`, `SalesExecutive`; lockout after 5 failed attempts; strict password policy
- **Dashboard** — 8 KPI cards + 3 Chart.js charts, with date-range filters and role-scoped data
- **Customers** — full CRUD, soft-delete (deactivate), email/phone uniqueness with Indian phone normalization
- **Leads** — state machine (New → Contacted → Qualified → Converted/Unqualified/Lost); one-transaction **Convert** that creates a Customer + optional Opportunity
- **Opportunities** — amount/probability/close-date business rules, stage flow (Qualification → Proposal → Negotiation → Won/Lost) with one-step-back and reopen, plus a **kanban Pipeline view**
- **Follow-Ups** — upcoming/overdue/completed tabs; Complete / Reschedule / Cancel; no past planned dates
- **Activities** — activity tracking per customer/lead
- **Reports** — 8 reports (Customer, Lead, Follow-Up, Opportunity, Pipeline, Conversion, User Activity, Audit Log), each with filters, sorting, pagination and CSV export (`?format=csv`)
- **REST API** — `/api/*` JSON endpoints (cookie auth, DTOs only, standard error envelope, proper status codes)
- **Audit logging** — every mutation plus report views/exports is recorded (who, what, old/new values, IP)
- **Security** — global anti-forgery, rate-limited logins, security headers, friendly error pages (400/401/403/404/500)

Full feature list: [docs/FEATURES.md](docs/FEATURES.md) · API reference: [docs/API.md](docs/API.md) · Architecture: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) · Security: [docs/SECURITY.md](docs/SECURITY.md) · Testing: [docs/TESTING.md](docs/TESTING.md) · Acceptance checklist: [docs/ACCEPTANCE.md](docs/ACCEPTANCE.md)

## Tech stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 8 MVC (Razor views, Bootstrap) |
| ORM | Entity Framework Core 8 |
| Auth | ASP.NET Core Identity (cookie auth, roles) |
| Databases | SQL Server (default) · SQLite (local dev/test) — switchable via `DatabaseProvider` |
| Charts | Chart.js |
| Tests | xUnit (integration + unit) |

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- For the default database provider: SQL Server (LocalDB, Express, or full) reachable via the connection string in `appsettings.json`
- For local dev/test only: nothing extra — the SQLite provider needs no server

## Setup

```bash
# 1. Restore + build
cd /path/to/acxiomcrm
dotnet restore
dotnet build AcxiomCRM.sln
```

## Database configuration

The provider is selected by the `DatabaseProvider` setting in `src/AcxiomCRM.Web/appsettings.json` (or the `DatabaseProvider` environment variable, which overrides it):

```json
"ConnectionStrings": {
  "SqlServer": "Server=localhost;Database=AcxiomCRM;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true",
  "Sqlite": "Data Source=acxiomcrm.db"
},
"DatabaseProvider": "SqlServer"
```

### Option A — SQL Server (default, production-like)

1. Edit `ConnectionStrings:SqlServer` in `appsettings.json` (or set it via environment variables / user secrets — **never commit real credentials**).
2. Apply the shipped migration:
   ```bash
   cd src/AcxiomCRM.Web
   dotnet ef database update
   ```
   (The design-time factory defaults to SQL Server; override with `EF_PROVIDER=Sqlite` if you want SQLite-flavored SQL instead.)
3. Run the app — `DbSeeder` calls `MigrateAsync()` on startup, so the schema is created/updated automatically even if you skip step 2.

### Option B — SQLite (local dev / quick verification)

```bash
cd src/AcxiomCRM.Web
DatabaseProvider=Sqlite dotnet run
```

No migration needed: on startup the app calls `EnsureCreatedAsync()` for SQLite (the shipped `InitialCreate` migration is SQL-Server-flavored and not applicable to SQLite) and creates `acxiomcrm.db` next to the web project.

### Adding a migration

```bash
cd src/AcxiomCRM.Web
dotnet ef migrations add <Name>          # SQL Server flavor (default)
EF_PROVIDER=Sqlite dotnet ef migrations add <Name>   # SQLite flavor (separate set)
```

> Keep migrations per-provider: SQL Server migrations use SQL Server types/functions that SQLite cannot apply, which is why SQLite goes through `EnsureCreated` instead.

## How to run

```bash
cd src/AcxiomCRM.Web

# SQL Server (default) — uses appsettings.json connection string
dotnet run

# SQLite (local, zero setup)
DatabaseProvider=Sqlite dotnet run
```

Then open `https://localhost:5001` (or the URL shown in the console). The default route lands on the Dashboard; anonymous users are redirected to `/Account/Login`.

## Demo credentials

Seeded automatically on first run (`DbSeeder`):

| Role | Email | Password |
|---|---|---|
| Admin | `admin@acxiomcrm.local` | `Acxiom@123` |
| Manager | `manager@acxiomcrm.local` | `Acxiom@123` |
| SalesExecutive | `sales@acxiomcrm.local` | `Acxiom@123` |

Sample customers, leads, opportunities and follow-ups are seeded alongside the users. **Change these passwords before any real deployment.**

## Project structure

```
AcxiomCRM.sln
├── src/AcxiomCRM.Web/          # The web application
│   ├── Program.cs              # DI, Identity, middleware pipeline, rate limiting, headers
│   ├── Controllers/            # MVC controllers (thin; delegate to services)
│   ├── Controllers/Api/        # REST API controllers (/api/*, cookie auth, DTOs)
│   ├── Services/               # Business logic + interfaces (ICustomerService, ILeadService, …)
│   ├── Data/                   # ApplicationDbContext, DbSeeder, Migrations/
│   ├── Models/                 # EF entities (Customer, Lead, Opportunity, FollowUp, Activity, AuditLog)
│   ├── ViewModels/             # MVC form/list view models (never entities)
│   ├── DTOs/                   # API request/response DTOs + error envelope
│   ├── Views/                  # Razor views (Bootstrap UI, Chart.js on dashboard)
│   ├── Validation/             # Custom validation attributes (e.g. Indian phone)
│   ├── Helpers/                # PaginatedList, misc helpers
│   ├── wwwroot/                # Static assets
│   └── appsettings.json        # ConnectionStrings + DatabaseProvider
└── tests/AcxiomCRM.Tests/      # xUnit test suite (unit + integration)
```

## Testing

57 automated test cases (auth/authz, validation, CRUD/workflows, API) — see [docs/TESTING.md](docs/TESTING.md) for how to run them, including the sandbox workaround for `dotnet test`.

```bash
dotnet build tests/AcxiomCRM.Tests
# normal machines:
dotnet test
```

## Roles & data visibility

| | Admin | Manager | SalesExecutive |
|---|---|---|---|
| CRM records (customers, leads, …) | all | all (no team entity exists — sees all like Admin) | only own / assigned-to-me |
| Users & roles admin | ✅ | ❌ | ❌ |
| Audit log viewer | ✅ | ❌ | ❌ |
| Reports | all 8 | all except Audit Log | scoped subset |

## Troubleshooting

| Problem | Fix |
|---|---|
| SQL Server unreachable on first run | Switch to SQLite for local dev: `DatabaseProvider=Sqlite dotnet run`, or fix `ConnectionStrings:SqlServer` |
| `dotnet ef` not found | Install the EF tool once: `dotnet tool install -g dotnet-ef` |
| Locked out after 5 bad logins | Wait 15 minutes, or restart with a fresh SQLite file during dev |
| `dotnet test` hangs in this sandbox | Known issue — use the xUnit console-runner workaround in [docs/TESTING.md](docs/TESTING.md) |
| API returns 302 instead of JSON 401/403 | Only applies to `/api/*` paths; make sure the request URL starts with `/api` |
| 429 Too Many Requests on login | Rate limit: 10 login attempts/min/IP by design |
