# AcxiomCRM — Security

## Authentication

ASP.NET Core Identity with cookie authentication (`ApplicationUser : IdentityUser`). Login, logout, and registration live in `Controllers/AccountController.cs`; the API mirrors sign-in/out at `POST /api/auth/login` and `POST /api/auth/logout` (same cookie, see [docs/API.md](API.md)).

- **Password storage**: framework-managed hashes only (Identity's `PasswordHasher`). No custom password table (§17.4), no plaintext anywhere, and password hashes are never exposed in views, DTOs, API responses, or logs.
- **Registration**: creates a standard user; `SignIn.RequireConfirmedEmail = false` (no email sender is wired in this build).
- **Session**: HttpOnly cookie, `SameSite=Lax`, `SecurePolicy=SameAsRequest` (HTTPS in production), sliding expiration of 8 hours.
- **Deactivation**: admins can deactivate accounts (`IsActive` flag); a deactivated user gets `401 account_disabled` on login attempts and is audit-logged.
- **Anonymous protection**: every CRM controller requires authentication; anonymous requests to MVC pages get 302 → `/Account/Login`, and to `/api/*` get a JSON **401** (cookie events convert redirects for API paths, so API clients never receive HTML).

## Password policy

Configured in `Program.cs` (spec §6.2):

| Rule | Value |
|---|---|
| Minimum length | 8 |
| Require digit | yes |
| Require lowercase | yes |
| Require uppercase | yes |
| Require symbol (non-alphanumeric) | yes |
| Unique characters required | 4 |

Registration and password-change forms enforce this both client-side (unobtrusive validation) and server-side (Identity).

## Account lockout (brute-force control, layered)

1. **Identity lockout**: 5 consecutive failed attempts → account locked for **15 minutes** (`lockoutOnFailure: true` on both MVC and API login). Lockout events are audit-logged (`"Account Locked"`).
2. **Rate limiting**: the `login` fixed-window policy — **10 attempts per minute per client IP** — is applied to `POST /Account/Login` and `POST /api/auth/login` via `[EnableRateLimiting("login")]`. Excess requests get **429 Too Many Requests**. Smoke tests confirmed 429s after 10 attempts on both endpoints.

## Authorization

Role-based, enforced at both the controller and service layers (defense in depth):

| Role | Scope |
|---|---|
| `Admin` | Everything: CRM records, users/roles administration, audit log viewer, all reports |
| `Manager` | All CRM records (like Admin) + reports, **but** no user/role/security administration |
| `SalesExecutive` | Only own records: `AssignedToId == userId \|\| CreatedById == userId` |

- `[Authorize(Roles = RoleNames.Admin)]` guards `UsersController`, `RolesController`, and the Audit Log report action.
- Record-level scoping is implemented in the service layer via the `SeesAllRecords` flag (`Admin || Manager`); a sales executive requesting another user's record gets `404 not_found`, and cannot assign records to other users (assignee is forced to self).
- **Known deviation from spec**: the spec describes Manager as "team scope", but there is no team entity in the data model — Manager sees all CRM records like Admin, except user/role/security administration. Documented as an accepted deviation in [docs/ACCEPTANCE.md](ACCEPTANCE.md) item 10.

## Anti-forgery (CSRF)

- `AutoValidateAntiforgeryTokenAttribute` is registered **globally** for MVC, so every state-changing form POST requires a valid token (`@Html.AntiForgeryToken()` in Razor forms via the form tag helper).
- API controllers opt out explicitly with `[IgnoreAntiforgeryToken]` on `CrmApiBaseController` — APIs authenticate via the cookie and are intended for programmatic clients; CSRF is a browser-form threat, hence the split.

## Security headers

Applied to **every** response by inline middleware in `Program.cs`:

| Header | Value |
|---|---|
| `X-Content-Type-Options` | `nosniff` |
| `X-Frame-Options` | `DENY` |
| `Referrer-Policy` | `strict-origin-when-cross-origin` |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=()` |

HSTS is enabled in non-development environments; `UseHttpsRedirection()` is always on.

## Input validation & injection resistance

- **No SQL injection surface**: all data access goes through EF Core parameterized queries; user input is never concatenated into SQL.
- **Server-side validation**: DataAnnotations on ViewModels/DTOs + custom attributes (`[IndianPhone]`) + business rules in services (uniqueness, state machines, amount/probability/date rules). Client-side unobtrusive validation mirrors the annotations for UX, but the server never trusts it (spec §17.8 — verified by tests that bypass the browser).
- **Error handling**: unhandled exceptions render friendly status pages (400/401/403/404/500 views) or the `{error:{code,message,details}}` API envelope — stack traces and database details are never returned to clients.

## Audit logging

`IAuditService` records security and business events into the `AuditLog` table: `UserId`, `Action`, `EntityName`, `RecordId`, `OldValue`, `NewValue`, `CreatedDate`, `IpAddress`.

Logged events include: Login Success/Failed, Account Locked, User Registered, role changes, password/security admin actions, every CRM mutation (create/update/delete/deactivate/convert/stage change/complete/reschedule/cancel), and **report views and CSV exports**. The audit viewer is Admin-only.

## Secrets handling

- No secrets in code or code comments. Connection strings live in `appsettings.json` (placeholders) and are meant to be overridden by environment variables or user secrets in real deployments.
- Demo credentials appear only in this documentation (`README.md` and `docs/`); they are seeded by `DbSeeder` with Identity-hashed passwords, and the password value itself is not stored anywhere in the codebase.

## Not included (future work)

- **Email confirmation** — disabled (`RequireConfirmedEmail = false`); no SMTP sender is wired.
- **Two-factor authentication** — not implemented.
- **Password reset via email** — depends on the above; admins can reset/change passwords through user administration.
- **General API throttling** — only the login endpoints are rate-limited; a global throttle would be a production addition.
- **Team entity for Manager scope** — Manager currently sees all records; a team hierarchy would refine this to true team scope.
