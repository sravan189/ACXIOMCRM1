# AcxiomCRM — Final Acceptance Checklist (§17.19)

Each of the 14 acceptance scenarios from spec §17.19, with PASS/FAIL and one line of evidence (test names and/or smoke-test observations). All evidence below was verified against the repo code, the xUnit suite (58 cases, all passing — see [docs/TESTING.md](TESTING.md)), and the coordinator's smoke-test observations.

| # | Scenario (spec §17.19) | Result | Evidence |
|---|---|---|---|
| 1 | Open AcxiomCRM without authentication → protected CRM pages must not be accessible | ✅ PASS | Smoke tests: anonymous MVC requests 302 → `/Account/Login`; anonymous API requests get 401 JSON (no redirect). Test: `ApiAuthTests.Anonymous_GetCustomers_Returns401_NotRedirect` |
| 2 | Register/login with a valid test user → user reaches the Dashboard | ✅ PASS | Test: `ApiAuthTests.Login_CorrectAdminCreds_Returns200_WithAdminRole`; MVC `POST /Account/Login` redirects to `Dashboard/Index` (the default route); smoke tests: dashboard 200 when authed |
| 3 | Create a Customer with invalid email/phone → client-side validation appears and save is blocked | ✅ PASS | Razor forms use `asp-validation-for` tag helpers (`Views/Customers/_CustomerForm.cshtml`, `Views/Leads/_LeadForm.cshtml`) with unobtrusive validation bound to `[EmailAddress]` / `[IndianPhone]` / `[Required]` annotations |
| 4 | Bypass browser validation with a crafted request → server-side validation still rejects invalid data | ✅ PASS | `[ApiController]` deliberately not used; `CrmApiBaseController.ValidModel` manually checks `ModelState` → 400 `validation_failed` envelope. Test: `ApiCustomerTests` invalid-payload cases → 400 |
| 5 | Create an Opportunity with Amount ≤ 0 → rejected | ✅ PASS | Test: `OpportunityServiceTests` (open-deal amount ≤ 0 rejected); smoke-tested API 400 flow |
| 6 | Create an Opportunity with Probability > 100 → rejected | ✅ PASS | Test: `OpportunityServiceTests` (probability 0–100 rule) |
| 7 | Set Expected Close Date in the past → rejected for an active opportunity | ✅ PASS | Test: `OpportunityServiceTests` (past close-date rule on active deals) |
| 8 | Create a Follow-Up dated before today → rejected for a new/planned follow-up | ✅ PASS | Test: `FollowUpServiceTests` (past planned date rejected); smoke-tested API 400 flow |
| 9 | Login as SalesExecutive → only authorized/assigned sales scope is accessible | ✅ PASS | Test: `CustomerServiceTests.GetPagedAsync_ScopesSalesExecutiveToOwnRecords`; verified behavior: sales gets 404 on others' records; assignee forced to self on writes |
| 10 | Login as Manager → team pipeline/report access is available | ✅ PASS (accepted deviation) | `SeesAllRecords = Admin \|\| Manager` in both base controllers; Manager can open all 8 reports (except the Admin-only audit viewer) and the kanban pipeline. **Deviation**: spec says "team scope" but no team entity exists — Manager sees all CRM records like Admin, minus user/role/security admin (documented in [docs/SECURITY.md](SECURITY.md)) |
| 11 | Login as Admin → user/role/audit administration is available | ✅ PASS | `UsersController` / `RolesController` guarded by `[Authorize(Roles = Admin)]`; Audit Log report action `[Authorize(Roles = Admin)]`; smoke-tested admin pages 200 |
| 12 | Create/update/delete a CRM record → appropriate audit entry is generated | ✅ PASS | Test: `CustomerServiceTests.CreateAsync_WritesAuditLogRow`; services call `IAuditService.LogAsync` on every mutation; report views/exports also logged |
| 13 | Call `/api/customers` → authorized JSON response is returned | ✅ PASS | `ApiCustomerTests` (5 cases: list, get, create → 201, duplicate → 409, missing → 404); smoke tests verified API login/401/400/409/404 flows end-to-end |
| 14 | Open Dashboard → KPI cards and Chart.js charts display authorized data | ✅ PASS | `DashboardServiceTests` (KPI values, date filters, role scoping); `DashboardViewModel` carries the 8 spec KPIs (§17.11) + 3 chart datasets (§17.12); `Views/Dashboard/Index.cshtml` renders Chart.js 4.4.1 (CDN); smoke test: dashboard 200 authed |

**Overall: 14/14 PASS** — one accepted deviation (item 10, Manager "team scope" → all-records scope, no team entity in the model).

### How to re-run acceptance yourself

1. `DatabaseProvider=Sqlite dotnet run` from `src/AcxiomCRM.Web` (zero-setup provider).
2. Sign in as each demo user (`admin@acxiomcrm.local`, `manager@acxiomcrm.local`, `sales@acxiomcrm.local` — password `Acxiom@123`; demo credentials live only in this documentation set).
3. Walk items 1–14 above in the browser (forms exercise client validation; crafting requests with curl exercises server validation, API codes, and role scoping).
4. Run the automated suite per [docs/TESTING.md](TESTING.md) for the service/API-level evidence.
