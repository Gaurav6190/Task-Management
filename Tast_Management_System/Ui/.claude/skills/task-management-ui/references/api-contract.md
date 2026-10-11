# TaskManagement.Api: what the UI depends on

Source of truth: `Tast_Management_System/Api/src/`. If anything below disagrees with the code there,
the code wins. Re-read the files named in each section and update the UI to match:
- **Auth:** `Controllers/AuthController.cs`, `Contracts/Auth/AuthContracts.cs`, `Application/Auth/Login/LoginCommand.cs`.
- **Tasks:** `Controllers/TasksController.cs`, `Contracts/Tasks/TaskRequests.cs`, `Application/Tasks/**`,
  `Domain/Tasks/TaskItem.cs`.

## Hosts (Properties/launchSettings.json)

| Profile | URL                                              |
| ------- | ------------------------------------------------ |
| https   | https://localhost:7213 (also http://localhost:5008) |
| http    | http://localhost:5008                            |

`app.UseHttpsRedirection()` is on, so plain http may redirect to https.

## CORS (appsettings.json)

`Cors:AllowedOrigins` = `["https://localhost:5173"]`. Note **https**. Vite serves **http**://localhost:5173 by
default, so direct browser calls from the dev server are blocked. The template avoids this with a Vite
`/api` proxy. Allowed methods: GET, POST, PUT, DELETE. Allowed headers: Authorization, Content-Type.

## POST /api/auth/login (anonymous)

Request (JSON, camelCase):

```json
{ "username": "user@example.com", "password": "secret" }
```

200 response:

```json
{ "accessToken": "eyJ...", "tokenType": "Bearer", "expiresAtUtc": "2026-10-11T10:00:00Z", "username": "user@example.com" }
```

Token lifetime is `Jwt:AccessTokenMinutes` (60). Every other endpoint requires
`Authorization: Bearer {accessToken}` and returns 401 without it.

Validation (`LoginCommandValidator`, FluentValidation):

- Username: required, at most 256 characters, must be an email address.
- Password: required, at most 128 characters.

## Error shape (Infrastructure/ApiProblems.cs)

Every error is `application/problem+json`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Invalid username or password.",
  "instance": "/api/auth/login",
  "code": "Auth.InvalidCredentials",
  "traceId": "00-...",
  "errors": { "Username": ["Username must be a valid email address."] }
}
```

`errors` appears only on 400 validation failures, with C#-style keys (`Username`, `Password`).

Codes the login page can receive (`Application/Abstractions/ErrorCodes.cs`):

| Status | code                       | Meaning                                       |
| ------ | -------------------------- | --------------------------------------------- |
| 400    | `Validation.Failed`        | Field errors in `errors`                       |
| 400    | `Request.Invalid`          | Body missing or not valid JSON                 |
| 401    | `Auth.InvalidCredentials`  | Wrong email or password (deliberately the same) |
| 401    | `Auth.Unauthorized`        | Missing or expired token (non-login endpoints) |
| 500    | `Server.Error` / `Database.Error` | Server fault                            |

## Tasks (Controllers/TasksController.cs), all need `Authorization: Bearer {accessToken}`

| Operation | Method & route | Success | Errors |
|---|---|---|---|
| List | `GET /api/tasks?status=&priority=&page=1&pageSize=20` | 200 `PagedResult<TaskDto>` | 400, 401 |
| Summary | `GET /api/tasks/summary` | 200 | 401, 500 |
| Get one | `GET /api/tasks/{id}` | 200 `TaskDto` | 401, 404 |
| Create | `POST /api/tasks` | 201 `{ id }` + `Location` | 400, 401 |
| Update | `PUT /api/tasks/{id}` | 204 | 400, 401, 404, 409 |
| Soft delete | `DELETE /api/tasks/{id}` | 204 | 401, 404 |

**TaskDto** (`Application/Tasks/TaskDto.cs`):

```json
{
  "id": "guid", "title": "…", "description": "…|null", "status": "ToDo|InProgress|Done",
  "priority": "Low|Medium|High|Critical", "assignedTo": "…|null", "dueDateUtc": "ISO|null",
  "createdDate": "ISO", "modifiedDate": "ISO|null", "completedAtUtc": "ISO|null"
}
```

**PagedResult:** `items`, `page`, `pageSize`, `totalCount`, `totalPages`. `pageSize` is 1–100
(`ListTasksQueryValidator`), and results come newest first. There is **no sort parameter**.

**Enums** travel as names (`"InProgress"`), never numbers. The JSON converter disallows integers.
`TaskItemConfiguration` stores them as text in SQL, so the database shows the same names.

**Create** (`CreateTaskRequest`): `title`, `description`, `priority`, `assignedTo`, `dueDateUtc`. New tasks
always start as `ToDo`. The validator (`CreateTaskCommandValidator`, `TaskValidationRules`) checks:
- `title`: required, ≤ 200 (`TaskItem.TitleMaxLength`),
- `description`: ≤ 4000,
- `assignedTo`: ≤ 100,
- `priority`: required, a valid name,
- `dueDateUtc`: not before the current **UTC** date.

**Update** (`UpdateTaskRequest`): `title`, `description`, `status`, `priority`, `assignedTo`, `dueDateUtc`.
**A full replacement:** every field must be sent. `status` and `priority` are required, so a partial
body fails with 400.
- The update validator has no due-date rule. The domain (`TaskItem.SetDetails`) only rejects a past due
  date when the value **changes**, so send the stored `dueDateUtc` back unchanged.
- The domain sets `ModifiedDate` on every update, and sets or clears `CompletedAtUtc` when the status
  moves to or from `Done`.
- 409 comes from the `RowVersion` concurrency token. It isn't exposed in the DTO, so it only fires when
  two saves race on the server.

**Database:** the `Tasks` table in `TaskManagementDb` (LocalDB in development), soft-deleted rows filtered
out by a global query filter. To confirm a UI change was saved:

```sql
SELECT Title, Status, Priority, ModifiedDate, CompletedAtUtc FROM Tasks ORDER BY ModifiedDate DESC;
```

## Test users

Users come from the `Auth:Users` configuration (`Infrastructure/Auth/ConfiguredUserAuthenticator.cs`)
and only work in the Development environment. They live in the API's user secrets
(`dotnet user-secrets list --project src/TaskManagement.Api`) and are added with
`Api/scripts/Set-DevAuthUser.ps1`. Only a **password hash** is stored, so the password has to come from
the user. Don't add or change users yourself, and never hard-code credentials in the UI.
