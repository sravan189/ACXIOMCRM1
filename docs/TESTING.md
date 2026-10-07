# AcxiomCRM — Testing Guide

## Running the suite

### Normal machines

```bash
export PATH=~/dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
cd ~/workspace/acxiomcrm
dotnet build tests/AcxiomCRM.Tests
dotnet test
```

### This sandbox (workaround)

`dotnet test` is **broken in this sandbox**: the testhost process hangs trying to open a socket through the proxy and never reports back. The workaround is the xUnit console runner checked in at `tests/.tools/xunit-console/` (this runs the same built test assembly without testhost):

```bash
export PATH=~/dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
cd ~/workspace/acxiomcrm
dotnet build tests/AcxiomCRM.Tests -c Release
DOTNET_ROLL_FORWARD=LatestMajor dotnet tests/.tools/xunit-console/xunit.console.dll \
  tests/AcxiomCRM.Tests/bin/Release/net8.0/AcxiomCRM.Tests.dll
```

(`DOTNET_ROLL_FORWARD=LatestMajor` is needed because the checked-in runner's `runtimeconfig.json` targets an old framework; roll-forward lets it run on the .NET 8 runtime. The command above was adapted for this sandbox — on any normal machine, prefer plain `dotnet test`.)

## Current status

**58 test cases, all passing** (43 `[Fact]`s + 2 `[Theory]`s expanded into 15 parameterized rows — see accounting below).

| Test class | Cases | What it covers |
|---|---|---|
| `ApiAuthTests` | 4 | Anonymous API call → 401 JSON (not 302); wrong-password login → 401; admin login → 200 with `Admin` role; lockout after 5 failed attempts (correct password still 401) |
| `ApiCustomerTests` | 5 | API customer CRUD, duplicate email/phone → 409, invalid payload → 400, 404 on missing |
| `ApiLeadConvertAndPipelineTests` | 2 | Lead convert via API (customer + opportunity created in one transaction); pipeline report endpoint shape |
| `CustomerServiceTests` | 5 | Duplicate email rejected; duplicate phone rejected **across different formatting** (normalization); SalesExecutive sees only own records; soft-delete sets inactive status; create writes an audit row |
| `DashboardServiceTests` | 3 | KPI values, date-range filtering, role scoping |
| `FollowUpServiceTests` | 5 | Past planned date rejected; complete/reschedule/cancel flows; upcoming vs overdue classification |
| `IndianPhoneAttributeTests` | 16 | 1 `[Fact]` + 2 `[Theory]`s: 8 valid formats (`+91` prefix, spaces, dashes) + 7 invalid (short, letters, wrong leading digit, empty) |
| `LeadServiceTests` | 8 | State machine (legal/illegal transitions, `Converted` only via Convert), convert requires Qualified, convert creates customer + opportunity atomically |
| `OpportunityServiceTests` | 7 | Amount ≤ 0 rejected on open deals; probability > 100 rejected; past close date rejected; stage flow (forward, one-step-back, Won/Lost → Qualification reopen) |
| `ReportServiceTests` | 3 | Customer/lead/opportunity report aggregation, filters, CSV-ready paging |

Case accounting: 43 `[Fact]` attributes + 2 `[Theory]` attributes with 15 `[InlineData]` rows → 43 + 15 = **58** executed cases (verified by an independent xUnit console run: `Total: 58, Errors: 0, Failed: 0, Skipped: 0`).

## Test project structure

```
tests/AcxiomCRM.Tests/
├── AcxiomCRM.Tests.csproj        # xunit 2.5.3, Microsoft.AspNetCore.Mvc.Testing, EF Core Sqlite
├── TestDb.cs                     # Unit-test DB helper: fresh SQLite file in /tmp per test, EnsureCreated, deleted on dispose
├── CrmApiFactory.cs              # WebApplicationFactory<Program> for API integration tests
│                                 # — overrides DatabaseProvider=Sqlite with a unique /tmp .db file
│                                 # — includes a CookieJarHandler so cookie-auth login works across requests
├── ApiAuthTests.cs / ApiCustomerTests.cs / ApiLeadConvertAndPipelineTests.cs
├── CustomerServiceTests.cs / LeadServiceTests.cs / OpportunityServiceTests.cs
├── FollowUpServiceTests.cs / DashboardServiceTests.cs / ReportServiceTests.cs
└── IndianPhoneAttributeTests.cs
```

## Test database strategy

- **Unit/service tests** use `TestDb.Create()`: a unique SQLite file under `/tmp` (`acxiom-test-<guid>.db`), schema built with `EnsureCreated()`, **deleted on dispose**. Tests never touch the dev `acxiomcrm.db`.
- **API integration tests** use `CrmApiFactory` (`WebApplicationFactory<Program>`): sets `DatabaseProvider=Sqlite` and points the SQLite connection string at a unique `/tmp` file (`acxiom-apitest-<guid>.db`), so the full app boots (Identity, middleware, seeding) against an isolated database.
- SQLite is the test provider because the shipped EF migration is SQL-Server-flavored; the same reason the app itself uses `EnsureCreated` for SQLite.

## How to add tests

1. **Service tests**: create a test class, use `using var db = TestDb.Create();` (optionally `TestDb.SeedUsersAsync(db, "user1")` to satisfy CreatedBy/AssignedTo FKs), instantiate the service with the context, and assert.
2. **API tests**: implement `IClassFixture<CrmApiFactory>`, use `factory.CreateClient()` with the `CookieJarHandler` to log in via `POST /api/auth/login`, then call endpoints and assert status codes + the `{error:{code,message,details}}` envelope.
3. **Validation tests**: for a new `ValidationAttribute`, follow `IndianPhoneAttributeTests` — `[Theory]` + `[InlineData]` rows for accepted/rejected inputs.

Guidelines:

- One class per service/controller under test; method names read as `Method_Scenario_Expected`.
- Assert business rules at the **service** layer (fast) and wire them at the **API** layer (status codes) — not both for everything.
- Keep tests hermetic: no network, no shared files, no dependence on seed data (each test builds its own rows).
- After adding tests, run the full suite and update the table in this file.
