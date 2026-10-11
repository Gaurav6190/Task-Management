# Task Management API: Work Items

How to use this file: one section per work item, with a checkbox per sub-task. Tick items as they are done and add new work items at the bottom with the next ID. All work follows the project skill `.claude/skills/task-mgmt-api` (.NET 8 only, DDD layering, EF Core 8 code-first, SQL Server).

**Status legend:** `[ ]` not started · `[~]` in progress · `[x]` done

---

## Decisions (agreed 2026-10-11)

| # | Topic | Decision |
|---|---|---|
| D1 | Routes | All endpoints use **`/api/tasks`**, with no version segment. |
| D2 | Status values | **`ToDo` / `InProgress` / `Done`**. Existing rows were converted by the migration (Open → ToDo, Completed → Done). |
| D3 | Extra endpoints | `/start`, `/complete`, `/reopen` are **removed**. Status changes go through `PUT /api/tasks/{id}`. |
| D4 | Field names | `CreatedAtUtc` was renamed to **`CreatedDate`**, and `ModifiedDate` was added. Values are stored and returned in UTC. |
| D5 | Summary contents | **Defined and implemented in T7b:** counts grouped by Status and Priority from one raw SQL query, plus totals. |
| D6 | `DueDateUtc` | Kept as an optional field (not in the spec, but already present). |

---

## T1: Task entity (EF Core code-first) [x]

The Task entity must contain at minimum: `Id`, `Title`, `Description`, `Status` (ToDo, InProgress, Done), `Priority` (Low, Medium, High, Critical), `AssignedTo` (string), `CreatedDate`, `ModifiedDate`, `IsDeleted`.

**Domain** (`src/TaskManagement.Domain/Tasks/`)
- [x] `TaskItemStatus` = `ToDo, InProgress, Done`.
- [x] `TaskPriority` = `Low, Medium, High, Critical` (unchanged).
- [x] `AssignedTo` (`string?`, max 100 chars; blank is stored as null).
- [x] `CreatedAtUtc` → `CreatedDate`; `ModifiedDate` (`DateTime?`) is set on every update and on delete.
- [x] `IsDeleted` + `DeletedDate`, plus a `Delete(nowUtc)` domain method. Deleted tasks can't be updated or deleted again.
- [x] `Update(...)` includes Status (replaces `Start` / `Complete` / `Reopen`) and keeps `CompletedAtUtc` in sync when moving to or from `Done`.
- [x] Domain unit tests (13): AssignedTo, ModifiedDate, status changes, soft delete, and an overdue task can still be edited.

**Infrastructure**
- [x] `TaskItemConfiguration`: `AssignedTo` max 100 plus an index, a `Priority` index, and the global query filter `HasQueryFilter(t => !t.IsDeleted)`.
- [x] Migration `20261011083425_AddAssignedToAndSoftDelete`. It was reviewed: `RenameColumn` for CreatedDate (no data loss), plus hand-added SQL to convert Status values in `Up()` and back in `Down()`.
- [x] Applied to LocalDB `TaskManagementDb`. Verified with rows in the old format (Open → ToDo, Completed → Done). `has-pending-model-changes` reports no changes.

---

## T2: Task APIs [x]

All endpoints live in `src/TaskManagement.Api/Controllers/TasksController.cs`, each with its own Application use case.

### T2a: List tasks with optional filtering by status and priority [x]
`GET /api/tasks?status=ToDo&priority=High&page=1&pageSize=20`
- [x] Optional `status` and `priority` filters, which can be combined.
- [x] Paged envelope (`items`, `page`, `pageSize`, `totalCount`, `totalPages`), `pageSize` capped at 100, newest first.
- [x] Deleted tasks are excluded.
- [x] 200, or 400 for an invalid filter or paging value.

### T2b: Get a single task by ID [x]
`GET /api/tasks/{id}`
- [x] Returns all fields except `IsDeleted`.
- [x] 200, or 404 when the task is missing or soft-deleted.

### T2c: Create a new task [x]
`POST /api/tasks`. Body: `title` (required, ≤ 200), `description` (≤ 4000), `priority`, `assignedTo` (≤ 100), `dueDateUtc`.
- [x] Starts in `ToDo`, with `CreatedDate` = now (UTC) and `IsDeleted = false`.
- [x] 201 with a `Location` header and `{ id }`, or 400 on validation failure.

### T2d: Update an existing task [x]
`PUT /api/tasks/{id}`. Body: `title`, `description`, `status`, `priority`, `assignedTo`, `dueDateUtc`.
- [x] Sets `ModifiedDate`.
- [x] 204; 400 (validation or invalid status); 404 (missing or deleted); 409 on a concurrency conflict (`RowVersion`).

### T2e: Soft-delete a task [x]
`DELETE /api/tasks/{id}`
- [x] Sets `IsDeleted = true`, `DeletedDate`, and `ModifiedDate`. **The row stays in the database** (verified in SQL).
- [x] `ITaskItemRepository.Remove` was removed, so a hard delete isn't possible through the code.
- [x] 204; 404 if the task is missing or already deleted.

### T2f: Task summary [x]
`GET /api/tasks/summary`
- [x] Endpoint, query, and handler created (`Application/Tasks/GetTaskSummary/`). Returns **200 `{}`** for now.
- [x] The route doesn't collide with `{id}` (the `{id:guid}` constraint); verified.
- [x] Summary contents defined and implemented in **T7b**.

### T2: shared items [x]
- [x] Removed the `/start`, `/complete`, `/reopen` endpoints and their use cases.
- [x] Route changed to `api/tasks`; old `api/v1/...` routes return 404.
- [x] `TaskManagement.Api.http` updated with a request for every endpoint.
- [x] Application unit tests (6): create, update, update not found, soft delete, delete twice.
- [x] `dotnet build`: 0 warnings, 0 errors. `dotnet test`: 22/22 passing (13 domain, 6 application, 3 architecture).
- [x] Smoke test of every endpoint against LocalDB.
- [x] Project skill updated (routes, soft delete, status via PUT, field names).

---

## Endpoint overview (current)

| Operation | Method & route | Success | Errors |
|---|---|---|---|
| List (filter by status, priority) | `GET /api/tasks` | 200 | 400 |
| Summary (raw SQL) | `GET /api/tasks/summary` | 200 | 500 `Database.Error` |
| Get by ID | `GET /api/tasks/{id}` | 200 | 404 |
| Create | `POST /api/tasks` | 201 | 400 |
| Update | `PUT /api/tasks/{id}` | 204 | 400, 404, 409 |
| Soft delete | `DELETE /api/tasks/{id}` | 204 | 404 |
| **Login** (anonymous) | `POST /api/auth/login` | 200 + access token | 400, 401, 415 |

Every `/api/tasks` endpoint requires `Authorization: Bearer <token>` from the login (D15); without a valid token they return **401** `Auth.Unauthorized`.

---

## Backlog (add new work items below)

- [x] ~~**T3: Define and implement the task summary**~~ (follow-up to T2f, D5). Done in **T7b**.

---

## T4: Basic input validation and enums [x]

**Goal:** every request is validated before it reaches the domain, and invalid input returns one consistent 400 response that tells the client exactly which field is wrong.

**Before (checked 2026-10-11):** three different 400 formats; a bad enum string leaked the internal type name `TaskManagement.Api.Contracts.Tasks.CreateTaskRequest`; malformed JSON leaked parser details; there were no per-field errors.

**Enums** (`src/TaskManagement.Domain/Tasks/`)
- [x] `TaskItemStatus { ToDo, InProgress, Done }` and `TaskPriority { Low, Medium, High, Critical }`, with XML docs.
- [x] Enum values are validated as part of validation (D8): request contracts carry them as strings, and the validators accept **member names only** (case-insensitive, e.g. `"high"`). Numbers (`2`, `"2"`) and flag combinations (`"Low, High"`) are rejected. Enum names are listed in the contract XML docs.

**Validation rules** (`Application/Tasks/**/…Validator`, shared rules in `TaskValidationRules.cs`)
- [x] Create: `Title` required (not blank), ≤ 200; `Description` ≤ 4000; `AssignedTo` ≤ 100; `Priority` required and a valid name; `DueDateUtc` not in the past.
- [x] Update: same as Create plus `Status` required and a valid name. No past-due-date rule, so overdue tasks stay editable; the domain still rejects a *changed* date in the past.
- [x] List: `Status` / `Priority` valid if given; `Page` ≥ 1; `PageSize` 1–100.
- [x] Messages such as `"Title is required."` and `"Status must be one of: ToDo, InProgress, Done."`. Field keys use C# names (`Title`, D10).
- [x] Domain invariants in `TaskItem` stay as the last line of defence.

**One validation response for everything**
- [x] FluentValidation failures → `errors` dictionary per field (`Error.ValidationErrors`).
- [x] `ApiBehaviorOptions.InvalidModelStateResponseFactory` → `ModelStateProblems` uses the same format for model-binding errors (bad JSON, wrong JSON type, non-numeric `page`), with C#-style field names and the noise entries removed.
- [x] `AllowInputFormatterExceptionMessages = false`, so no type names or byte positions reach clients.

**Tests**
- [x] 18 validator unit tests (`TaskValidatorTests.cs`).
- [x] 8 integration tests for validation responses (see T5).

---

## T5: Global exception handling and consistent error responses [x]

**Goal:** every error from every endpoint returns **the same JSON format** with the correct HTTP status, a stable **error code**, and a human-readable message.

**Standard error format** (RFC 7807 ProblemDetails plus `code` and `traceId`; all built by `Api/Infrastructure/ApiProblems.cs`):
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "One or more fields are invalid.",
  "instance": "/api/tasks",
  "errors": { "Title": ["Title is required."], "Priority": ["Priority must be one of: Low, Medium, High, Critical."] },
  "code": "Validation.Failed",
  "traceId": "00-ee0cd9c6b2132b835d3dc4c6f31f4877-c670aa372b529524-00"
}
```
`errors` appears only for 400 validation and request errors. Content type is always `application/problem+json`.

**Error codes** (`Application/Abstractions/ErrorCodes.cs`, format `Area.Reason`, D7)

| Code | HTTP | When |
|---|---|---|
| `Validation.Failed` | 400 | FluentValidation failure, or an invalid query/route value |
| `Request.Invalid` | 400 | Malformed JSON, wrong JSON type, missing body, `BadHttpRequestException` |
| `Domain.RuleViolated` | 400 | `DomainException` (can carry a more specific code) |
| `Task.NotFound` | 404 | Task missing or soft-deleted |
| `Route.NotFound` | 404 | No endpoint matches the URL |
| `Method.NotAllowed` | 405 | Wrong HTTP verb for the route |
| `Request.UnsupportedMediaType` | 415 | Body sent without `application/json` |
| `Concurrency.Conflict` | 409 | `DbUpdateConcurrencyException` (RowVersion) |
| `Database.Error` | 500 | `DbUpdateException` (details only in the log) |
| `Server.Error` | 500 | Any other unhandled exception |

**Implementation**
- [x] `ErrorCodes` catalogue; `TaskErrors` and `Error.Validation` use it. `DomainException` has a `Code` (default `Domain.RuleViolated`).
- [x] `ApiProblems` is the single builder for every error response (`Create`, `Enrich`, `ToResult`), used by `ResultExtensions.ToProblem()`, `ModelStateProblems`, `GlobalExceptionHandler`, and `CustomizeProblemDetails`.
- [x] `GlobalExceptionHandler` maps the exceptions in the table. Client-cancelled requests are logged at Information and get no body.
- [x] Logging: 5xx at Error with the exception, 4xx at Warning without a stack trace. Every log entry has `code` and `traceId`.
- [x] 5xx responses use a generic message only; no exception text, type names, stack traces, or SQL.
- [x] `UseStatusCodePages()`, so 404 unknown route, 405, and 415 get the standard format.
- [x] Removed `[Produces("application/json")]` from the controller. It was overwriting `application/problem+json` on every error, which the integration tests caught. Added a controller-level `[ProducesResponseType<ProblemDetails>(500)]`; 4xx responses are documented as ProblemDetails by `[ApiController]`.

**Tests**
- [x] New project `tests/TaskManagement.Api.IntegrationTests` (xUnit + `Microsoft.AspNetCore.Mvc.Testing` 8.0.31), running against the **real database** (D9). The tests only read; exception scenarios use a stubbed handler that throws.
- [x] 16 integration tests: one per error code and validation case. Each checks status, `code`, `title`, `type`, `instance`, `traceId`, and content type, and that no internals leak.

**Result:** 56/56 tests passing (13 domain, 24 application, 3 architecture, 16 integration). Build: 0 warnings.

---

## Decisions for T4/T5 (agreed 2026-10-11)

| # | Topic | Decision |
|---|---|---|
| D7 | Error code format | Dotted strings **`Area.Reason`** (e.g. `Task.NotFound`), defined once in `ErrorCodes`. |
| D8 | Enum values | Validated as **part of validation**. Invalid or numeric values return a field-level 400; names are accepted case-insensitively. |
| D9 | Integration test database | **Real database** (`TaskManagementDb`). Tests are read-only; write failures are simulated with stubs. |
| D10 | Field names in `errors` | **C# property names** (`Title`, `PageSize`). |

---

## T6: Local SQL Server (Windows auth), initial migration, and database verification [x]

**Goal:** the API runs against the **local SQL Server instance** using **Windows authentication**. The database is created by running the EF Core code-first migrations from the Web API project, and the tests (including integration tests against the database) prove that the API returns exactly what is stored in the database.

**Environment (checked 2026-10-11)**
- Local instance: default instance `MSSQLSERVER` on `localhost` (SQL Server 2022 Developer, 16.0.1000.6), service running, startup Automatic.
- Windows login `Karagir\User` connects with integrated security and is `sysadmin` (can create databases).
- Before T6 `TaskManagementDb` did not exist on this instance; development used LocalDB (`(localdb)\MSSQLLocalDB`).
- [x] The old LocalDB `TaskManagementDb` (0 rows) was dropped on 2026-10-11, including its `.mdf` and `.ldf` files. The local SQL Server `localhost` is now the only database.

**T6a: SQL connection changes**
- [x] Connection string `ConnectionStrings:Default` → `Server=localhost;Database=TaskManagementDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;Application Name=TaskManagement.Api`. No user name or password: Windows authentication.
- [x] Defined once in `appsettings.json`, and removed from `appsettings.Development.json` so there is a single source. Other environments override it with the environment variable `ConnectionStrings__Default`.
- [x] `TrustServerCertificate=True` is required because the local instance uses a self-signed certificate (Microsoft.Data.SqlClient encrypts by default). Don't use it for a shared or production server.
- [x] Startup fails fast with a clear message if the connection string is missing (already in `AddInfrastructure`).

**T6b: Run the initial migration from the Web API project**
- [x] `dotnet tool restore` (pinned `dotnet-ef` 8.0.31).
- [x] `dotnet ef migrations list` with `--startup-project src/TaskManagement.Api` shows both migrations (`InitialCreate`, `AddAssignedToAndSoftDelete`) as pending against the new server.
- [x] `dotnet ef database update --project src/TaskManagement.Infrastructure --startup-project src/TaskManagement.Api` ran `CREATE DATABASE [TaskManagementDb]` on `localhost` and applied both migrations.
- [x] Verify the schema: table `Tasks` with every T1 column, types, and lengths; indexes; `__EFMigrationsHistory` has 2 rows with ProductVersion `8.0.31`; `has-pending-model-changes` reports no changes.
- [x] Verified: starting the Web API logs `No migrations were applied. The database is already up to date.`

**T6c: Run the tests against the database**
- [x] `dotnet test`: **61/61 passing** against `localhost` (13 domain, 24 application, 3 architecture, 21 integration).
- [x] New integration tests (`DatabaseRoundTripTests`) that **compare API responses with what is actually stored in SQL Server**:
  - rows inserted straight into the database are returned by `GET /api/tasks/{id}` and `GET /api/tasks` with the same values (title, description, status, priority, assignedTo, UTC dates);
  - the status and priority filters return exactly the matching rows;
  - `POST` / `PUT` store exactly the values sent (read back directly from the database);
  - `DELETE` keeps the row with `IsDeleted = 1`, and the API no longer returns it.
- [x] Tests that write use a unique marker in the title (`[IT-xxxxxxxx]`) and delete their own rows in `Dispose`; the table had 0 rows after the run. Found and fixed during the work: `SqlParameter.AddWithValue(DateTime)` sends the legacy `datetime` type (about 3 ms precision), so the test helper now sends `datetime2`. This was a test-helper issue only; the API stores and returns dates exactly to the tick.

**T6d: Manual verification**
- [x] Ran the API, created/updated/deleted 3 tasks through the endpoints, and compared `GET /api/tasks` with `SELECT * FROM Tasks`: title, status, priority, assignedTo, and createdDate (to the tick) matched, with **0 mismatches**. The soft-deleted row stays in SQL with `IsDeleted = 1`, is not listed, and `GET` returns 404. The verification rows were removed afterwards.

**Acceptance criteria**
- `TaskManagementDb` exists on the local SQL Server, created only through EF Core migrations run from the Web API project.
- The API connects using Windows authentication only (no credentials in config).
- All tests pass, and the round-trip tests prove that API values match the database values.

---

## T7: Sample data seed and raw-SQL task summary [x]

**Goal:** (a) the database is filled with **at least 25 realistic sample tasks** in every environment, and (b) `GET /api/tasks/summary` returns the **count of tasks grouped by Status and Priority**, computed by **one raw SQL query that does not use EF Core**.

### Decisions for T7 (agreed 2026-10-11)

| # | Topic | Decision |
|---|---|---|
| D11 | Where sample data goes | **Every environment**, so it is delivered through an EF Core migration. |
| D12 | Summary shape | **Grouped counts (all 12 combinations, zeros included) plus `total`, `byStatus`, and `byPriority`**, all from the one query. |
| D13 | Seed format | A **`.sql` file of INSERT statements** that the migration runs. The raw summary query uses **plain ADO.NET** (no Dapper, no new package). |
| D14 | When seeding happens | **Together with table creation**: `SeedSampleTasks` is the migration after the table migrations, so a fresh `database update` (or API startup `MigrateAsync`) creates the tables and inserts the seed in one run. |

### T7a: Seed 30 sample tasks (EF migration + SQL file) [x]
- [x] `src/TaskManagement.Infrastructure/Persistence/Migrations/Sql/SeedSampleTasks.sql`: one `INSERT … SELECT … FROM (VALUES …)` statement for **30 tasks: 27 active + 3 soft-deleted**.
  - All **12 Status × Priority combinations** among the active tasks: ToDo 10 / InProgress 9 / Done 8; Low 7 / Medium 8 / High 7 / Critical 5.
  - Realistic titles and descriptions; assignees `alice`, `bob`, `carol`, `dave`, or unassigned; due dates in the future, overdue, or none.
  - Dates are relative to when the migration runs (`SYSUTCDATETIME()`): `CreatedDate` within the last 60 days; `ModifiedDate` for non-ToDo or deleted tasks; `CompletedAtUtc` for Done only; `DeletedDate` for deleted only.
  - Fixed Ids `5EED0000-0000-0000-0000-0000000000NN` (NN = 01–30) and `WHERE NOT EXISTS`, so re-running adds nothing and user rows are never touched.
  - No `GO` / `BEGIN TRAN`: EF runs each migration as one batch inside its own transaction.
- [x] `…/Migrations/Sql/RemoveSampleTasks.sql`: deletes only the seed Id range.
- [x] Migration `20261011093112_SeedSampleTasks`: `Up()` runs the seed file and `Down()` runs the remove file. Both are read from **embedded resources** (`<EmbeddedResource Include="Persistence\**\*.sql" />`, `SqlScripts.Read`), so the SQL is also included in `dotnet ef migrations script` output for production.
- [x] Applied to `localhost/TaskManagementDb`: 30 rows (27 active, 3 deleted).
- [x] Verified on a **fresh throwaway database**: one `database update` ran `CREATE DATABASE`, then InitialCreate, then AddAssignedToAndSoftDelete, then SeedSampleTasks, giving 30 rows. The throwaway database was dropped afterwards.
- [x] Verified **Down/Up**: reverting removed only the 30 seed rows (a user-created row was kept), and re-applying restored them.

### T7b: Raw SQL summary on `GET /api/tasks/summary` [x]
- [x] The query lives in `src/TaskManagement.Infrastructure/Persistence/Queries/Sql/TaskSummary.sql` (embedded; it can be run as-is in SSMS):
  ```sql
  SELECT t.Status, t.Priority, COUNT(*) AS TaskCount
  FROM dbo.Tasks AS t
  WHERE t.IsDeleted = 0
  GROUP BY t.Status, t.Priority
  ORDER BY t.Status, t.Priority;
  ```
- [x] `Infrastructure/Persistence/Queries/TaskSummaryQuery.cs` runs it with **ADO.NET** (`SqlConnection` / `SqlCommand` from `SqlConnectionFactory`, same Windows-auth connection string, 30 s timeout, `CancellationToken`). No EF Core in this path. Unknown Status/Priority values are logged and skipped.
- [x] Application: `ITaskSummaryQuery` + `GetTaskSummaryQueryHandler` fill missing combinations with 0 and add up `total`, `byStatus`, and `byPriority` from the same rows. `TaskSummaryDto` replaced the `{}` placeholder.
- [x] `GlobalExceptionHandler`: `SqlException` → 500 `Database.Error` (raw ADO.NET errors aren't `DbUpdateException`).
- [x] Architecture test: Application does not depend on `Microsoft.Data.SqlClient`.
- [x] Live response with the seed data: `total` 27; `byStatus` ToDo 10 / InProgress 9 / Done 8; `byPriority` Low 7 / Medium 8 / High 7 / Critical 5; 12 grouped cells, **identical to running TaskSummary.sql directly**.

### Tests [x]
- [x] Unit (3): zeros for all 12 combinations, enum order, totals equal the sum of the cells.
- [x] Integration `TaskSummaryTests` (4): the API equals an independent `GROUP BY` on SQL Server; 12 cells and consistent totals; inserted rows change the counts by exactly that amount, and deleted ones are ignored; soft delete through the API lowers the count.
- [x] Integration `SeedDataTests` (4): 30 seed rows (27 active / 3 deleted), all 12 combinations, consistent audit fields, and the API returns seed task 11 and hides deleted seed task 28.
- [x] Tests that write or count are in one xUnit collection (`[Collection("Database")]`), so they don't run in parallel.
- [x] **72/72 passing** (13 domain, 27 application, 3 architecture, 29 integration). Build: 0 warnings.

---

## T8: Authentication API (username + password) [x]

**Goal:** the React UI sends a **username (email) and password** once. The API returns a **JWT access token**, which the UI sends with every API call for 60 minutes.

### Decisions for T8 (agreed 2026-10-11)

| # | Topic | Decision |
|---|---|---|
| D15 | Which APIs need the token | **All of them.** Every endpoint requires `Authorization: Bearer <token>` except `POST /api/auth/login`. Log in **once** and reuse the same token for every call. |
| D16 | Test credentials | The username/password used for testing are **not stored anywhere in the repository** (no code, config, docs, or tests). Each developer configures them locally in **dotnet user-secrets** with `scripts/Set-DevAuthUser.ps1`, which stores only a **password hash**. Configured users work **only in Development**. |
| D17 | Token | **JWT, valid 60 minutes, no refresh token.** The UI logs in again when it gets a 401. |
| D18 | Username format | **Always an email address**, compared case-insensitively. |
| D19 | Login attempts | **No limit** (no rate limiting). |
| D20 | UI origin (CORS) | **`https://localhost:5173`** (React/Vite dev server) for now. Change `Cors:AllowedOrigins` in `appsettings.json` when the real UI URL is known. |

### T8a: Login endpoint [x]
`POST /api/auth/login` (anonymous). Body `{ "username": "<email>", "password": "<password>" }` → **200**:
```json
{ "accessToken": "eyJhbGciOi...", "tokenType": "Bearer", "expiresAtUtc": "2026-10-11T11:03:16Z", "username": "<email>" }
```

| Scenario | Status | `code` |
|---|---|---|
| Correct username + password (username in any case) | 200 | — |
| Username or password missing/blank | 400 | `Validation.Failed` (`errors.Username` / `errors.Password`) |
| Username not an email, longer than 256, or password longer than 128 | 400 | `Validation.Failed` |
| Unknown username **or** wrong password | 401 | `Auth.InvalidCredentials`, identical response for both (`"Invalid username or password."`) |
| Malformed JSON / wrong content type | 400 / 415 | `Request.Invalid` / `Request.UnsupportedMediaType` |

- [x] The password is never logged, echoed in errors, or returned (`ToString()` of command and request redacts it; verified by tests that inspect responses and logs).
- [x] Unknown usernames still run a hash verification, so response time doesn't reveal whether a username exists.

### T8b: All APIs protected [x]
- [x] Secure by default: the authorization **fallback policy** requires an authenticated user on every endpoint; only login is `[AllowAnonymous]`. New controllers are protected automatically.
- [x] Missing, invalid, expired, tampered, unsigned, or wrong-issuer/audience tokens → **401** in the standard error format, `code = "Auth.Unauthorized"`, detail pointing to `/api/auth/login`.
- [x] Because of the fallback policy, an anonymous request to an unknown route or with a wrong method also gets 401 (it doesn't reveal which routes exist). With a token it gets 404 / 405.
- [x] New codes in `ErrorCodes`: `Auth.InvalidCredentials`, `Auth.Unauthorized`, `Auth.Forbidden`.
- [x] Validation: issuer, audience, lifetime (1 min clock skew), signing key, HS256 only. `IncludeErrorDetails = false`.

### T8c: Implementation [x]
- [x] **Application:** `Auth/Login/LoginCommand` + validator + handler; `IUserAuthenticator`, `IAccessTokenService`; `AuthErrors`. No JWT/crypto/ASP.NET packages (architecture test).
- [x] **Infrastructure:** `Auth/ConfiguredUserAuthenticator` (ASP.NET Core Identity `PasswordHasher`, PBKDF2-HMACSHA512) and `Auth/JwtAccessTokenService` (HMAC-SHA256). `JwtOptions` is validated at startup (issuer, audience, 1–1440 minutes, key ≥ 32 bytes).
- [x] **Api:** `AuthController`, `Contracts/Auth`, `Infrastructure/AuthSetup.cs` (JwtBearer, fallback policy, CORS, Development-only users), Swagger "Authorize" button.
- [x] **Config:** `appsettings.json` → `Jwt:Issuer` `TaskManagement.Api`, `Jwt:Audience` `TaskManagement.Ui`, `Jwt:AccessTokenMinutes` 60, `Cors:AllowedOrigins` `https://localhost:5173`. **No signing key or users in any appsettings file.**
- [x] **Secrets:** `UserSecretsId` added to the Api project. Run `./scripts/Set-DevAuthUser.ps1 -Username <email> -Password <password>` once per machine: it stores the username, the password hash, and a random 64-byte `Jwt:SigningKey` (if missing) in user-secrets. Other environments set `Jwt__SigningKey` as an environment variable; startup fails if it's missing.
- [x] **Packages (8.0.x / net8.0):** `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.31 (Api); `Microsoft.Extensions.Identity.Core` 8.0.31, `Microsoft.IdentityModel.JsonWebTokens` 7.7.3, `Microsoft.Extensions.Options.ConfigurationExtensions` 8.0.0 (Infrastructure).
- [x] **CORS:** only `https://localhost:5173`; methods GET/POST/PUT/DELETE; headers `Authorization`, `Content-Type`.
- [x] `TaskManagement.Api.http`: login once (`@name login`), then `{{token}}` on every request; credentials are placeholders.
- [x] Verified live: the hash created by the script is accepted, login returns a token with `sub`/`email`/`jti`/`iss`/`aud` and `exp − iat = 3600 s`, the token works on every API, no or tampered token → 401, and only the UI origin passes CORS.

---

## T9: Unit and integration tests: positive and negative scenarios for every API [x]

**Goal:** every endpoint, including authentication, has tests for all positive and negative scenarios, written with **NUnit** (D21). The conventions are in **`Specs.md`**.

### Decision for T9 (agreed 2026-10-11)

| # | Topic | Decision |
|---|---|---|
| D21 | Framework and coverage | **NUnit** for all tests (all 5 test projects migrated from xUnit). **Unit-test line coverage above 90%.** Testing notes in `Specs.md`. |

### Results
- [x] All test projects migrated to **NUnit 4.4.0** (shared packages in `tests/Directory.Build.props`). NUnit 4.6 was tried and rejected: its new `Action` overloads are ambiguous under C# 12 (.NET 8 SDK); see Specs.md.
- [x] New project `tests/TaskManagement.Infrastructure.UnitTests`.
- [x] **304 tests, all passing; build 0 warnings.**

| Project | Tests | Covers |
|---|---|---|
| Domain.UnitTests | 38 | every `TaskItem` rule and boundary; `Entity`, `ValueObject`, `TaskItemId`, `DomainException` |
| Application.UnitTests | 103 | all 7 handlers (incl. login), all 4 validators (every rule ±, boundaries), `Result`/`Error`, `EnumNames`, paging math, DI registrations |
| Infrastructure.UnitTests | 21 | password verification (correct, wrong, case, unknown, malformed hash, script hash format), JWT claims/expiry/signature, embedded SQL scripts |
| ArchitectureTests | 4 | layer rules incl. "Application has no JWT/Identity/SqlClient/ASP.NET dependency" |
| Api.IntegrationTests | 146 | login (21), authorization: 6 endpoints × 8 bad-token kinds + valid tokens (56), CORS (9), tasks CRUD + paging + boundaries (38), errors (12), summary (5), seed (4), contracts (1) |

- [x] **Coverage (D21 target > 90%):**

| Scope | Line | Branch |
|---|---|---|
| **Unit tests only, Domain + Application** | **100%** | 95.9% |
| All tests, all production code | **97.3%** | 77.5% |
| per assembly (all tests) | Domain 100%, Application 100%, Infrastructure 99.4%, Api 93% | |

- [x] Fixed during the work: `MustBeEnumName` treated `""` as invalid while the handler treated it as "no filter". Both now treat blank as not provided (required fields still use `NotEmpty`).
- [x] Fixed during the work: the coverage settings first excluded `CompilerGeneratedAttribute`, which silently dropped every async method from the report. Removed; the numbers above include all async code.
- [ ] Known uncovered defensive branches (for a later task): client-cancelled requests and `BadHttpRequestException` in `GlobalExceptionHandler`; the `ErrorType.Failure` → 500 fallback in `ResultExtensions`; the 403/415 type URLs in `ApiProblems`; `DomainEventDispatcher` with registered handlers (none exist yet).

<!-- T10: ... -->
