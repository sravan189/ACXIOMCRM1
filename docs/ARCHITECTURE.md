# AcxiomCRM — Architecture

## Layered design

The application follows a strict thin-controllers → services → EF Core layering. Controllers never contain business rules or LINQ queries against `DbSet`s; they parse input, delegate to a service, and map results to the presentation model.

```
┌──────────────────────────────────────────────────────────────┐
│  Presentation                                                  │
│  Controllers/ (MVC, Razor views + ViewModels)                 │
│  Controllers/Api/ (JSON endpoints + DTOs)                     │
├──────────────────────────────────────────────────────────────┤
│  Business logic                                                │
│  Services/ — ICustomerService, ILeadService, IOpportunityService, │
│  IFollowUpService, IActivityService, IDashboardService,       │
│  IReportService, IAuditService                                │
├──────────────────────────────────────────────────────────────┤
│  Data access                                                   │
│  Data/ApplicationDbContext (EF Core), Models/ (entities),      │
│  Data/Migrations/ (SQL Server), Validation/ attributes        │
└──────────────────────────────────────────────────────────────┘
```

**Request flow (MVC):** `HTTP → Middleware → Controller → Service → EF Core → View(ViewModel)`.
**Request flow (API):** `HTTP → Middleware → ApiController → Service → EF Core → DTO JSON`.

## Folder layout

| Folder | Contents | Notes |
|---|---|---|
| `Controllers/` | 11 MVC controllers (Account, Dashboard, Customers, Leads, Opportunities, FollowUps, Activities, Reports, Users, Roles, Home) | Thin; inherit `CrmBaseController` for `CurrentUserId`/`SeesAllRecords` helpers |
| `Controllers/Api/` | 7 API controllers + `ApiExceptionFilter` + `CrmApiBaseController` | `[Authorize]`, `[IgnoreAntiforgeryToken]` on the base |
| `Services/` | 8 service interfaces + implementations, `ReportModels`, `LookupQueries`, `CrmServiceExceptions` | All registered as scoped in `Program.cs`; `AddDashboard()` extension wires dashboard deps |
| `Models/` | EF entities + `Enums.cs` + `RoleNames` | Customer, Lead, Opportunity, FollowUp, Activity, AuditLog, ApplicationUser |
| `Data/` | `ApplicationDbContext`, `DbSeeder`, `DesignTimeDbContextFactory`, `Migrations/` | Seeding + provider switch live here |
| `ViewModels/` | Per-module form/list view models (Account, Customers, Leads, …) | MVC-only; entities never reach views |
| `DTOs/` | API request/response DTOs, `DtoMappings`, `ApiErrorDto`, `PagedResultDto` | API-only; entities never serialized to JSON |
| `Validation/` | Custom `ValidationAttribute`s (e.g. `[IndianPhone]`) | Shared by ViewModels and DTOs |
| `Helpers/` | `PaginatedList<T>` | Paging primitive for MVC lists |
| `Views/` | Razor views per controller + `Shared/` layout + `Home/StatusCode.cshtml` | Bootstrap UI; Chart.js on the dashboard |
| `wwwroot/` | Static assets | CSS/JS |

## Key patterns

### ViewModels vs DTOs (presentation-model separation)
- **ViewModels** shape data for Razor views: form fields with `[Required]`/`[EmailAddress]`/etc., dropdown option lists, pager state, tab state. Used only by MVC actions.
- **DTOs** shape JSON for the API: `*CreateDto` / `*UpdateDto` request payloads, `*Dto` response projections via `DtoMappings`, and the standard `ApiErrorDto` envelope. Used only by API actions.
- EF entities cross neither boundary.

### `CrmServiceExceptions` (service-layer failures)
Services throw two domain exceptions instead of returning magic values:
- `CrmValidationException` — rule/validation failures; carries a list of `(Field, Message)` errors.
- `CrmNotFoundException` — record missing or outside the caller's scope.

Some services additionally return `(bool ok, string? error, …)` tuples for expected business failures (e.g. lead convert). The API translates everything:
- `CrmValidationException` → **400** `validation_failed` (field-keyed `details`)
- `CrmNotFoundException` → **404** `not_found`
- message contains "already exists" → **409** `duplicate`
- otherwise → **400** / **500** via `ApiExceptionFilter`

MVC controllers catch these and surface friendly messages + validation errors in the Razor views.

### `PaginatedList<T>` (MVC paging)
A `List<T>` subclass carrying `PageIndex`, `PageSize`, `TotalCount`, `TotalPages`, `HasPreviousPage`/`HasNextPage`. Services return it; views render the pager from it. The API equivalent is `PagedResultDto<T>` (`data/page/pageSize/totalCount/totalPages`).

### Role scoping (`SeesAllRecords`)
Both base controllers (`CrmBaseController`, `CrmApiBaseController`) expose:

```csharp
protected bool SeesAllRecords => IsAdmin || IsManager;
```

Every service read method takes `(userId, seesAllRecords)` and filters `AssignedToId == userId || CreatedById == userId` when `false`. Scoping lives in the service layer, so MVC and API cannot diverge. Writes additionally re-check visibility before mutating.

### Audit
`IAuditService.LogAsync(action, userId, userName, entityName, recordId, oldValue, newValue, ipAddress)` persists an `AuditLog` row. Services call it on every mutation (create/update/delete/deactivate/convert/stage change/complete/reschedule/cancel/role change); `AccountController` logs login/logout/lockout; report controllers log report views and CSV exports. The Audit Log viewer report is Admin-only.

## Provider-switch design

`DatabaseProvider` (`appsettings.json` or env var, default `SqlServer`) is read once at startup in `Program.cs`:

```csharp
if (dbProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    options.UseSqlite(...);
else
    options.UseSqlServer(...);
```

Schema strategy then diverges in `DbSeeder.SeedAsync`:
- **SQL Server** → `MigrateAsync()` — the shipped `InitialCreate` migration is SQL-Server-flavored; `dotnet ef` commands target it by default (`EF_PROVIDER` override exists for SQLite-flavored migrations).
- **SQLite** → `EnsureCreatedAsync()` — builds the schema from the current model, since the SQL-Server migration is not applicable.

No provider-specific code exists in services or controllers; they only see `ApplicationDbContext`.

## SQLite decimal-aggregation pattern

EF Core's SQLite provider cannot translate `decimal` `Sum`/`OrderBy` to SQL. Services therefore materialize first, then aggregate in memory:

```csharp
// DashboardService: fetch amounts, sum client-side
TotalPipelineValue = (await openOpps.Select(o => o.Amount).ToListAsync()).Sum(),
```

The same pattern (fetch → `ToListAsync` → sort/page/aggregate in memory) is used in `ReportService` for grouping/counting. This keeps one codebase working on both providers; the datasets are CRM-scale so the cost is negligible.

## Request pipeline (middleware order)

From `Program.cs` — order matters:

1. `UseExceptionHandler("/Home/Error")` / `UseDeveloperExceptionPage()` + `UseHsts()` (non-dev)
2. `UseHttpsRedirection()`
3. **Security-headers middleware** (inline): `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy: camera=(), microphone=(), geolocation=()` — on every response
4. `UseStaticFiles()`
5. `UseRouting()`
6. `UseRateLimiter()` — the `login` policy (10/min/IP) is opted into by `POST /Account/Login` and `POST /api/auth/login`
7. `UseAuthentication()` → `UseAuthorization()`
8. `UseStatusCodePagesWithReExecute("/Home/StatusCode", "?code={0}")` — friendly 400/401/403/404/500 views
9. `MapControllerRoute("{controller=Dashboard}/{action=Index}/{id?}")`

Other startup decisions in `Program.cs`:

- **Global anti-forgery**: `AutoValidateAntiforgeryTokenAttribute` is registered as a global MVC filter; API controllers opt out via `[IgnoreAntiforgeryToken]` on `CrmApiBaseController`.
- **Identity options**: password policy (min 8, digit/upper/lower/symbol, 4 unique chars), lockout (5 attempts → 15 min), `RequireUniqueEmail`.
- **Cookie hardening**: HttpOnly, `SameSite=Lax`, sliding 8-hour expiration; `/api/*` auth redirects are converted to 401/403 JSON instead of 302s.
- **Seeding**: `DbSeeder.SeedAsync` runs on every startup — idempotent (roles/users created only if missing) — so a fresh database is demo-ready immediately.
