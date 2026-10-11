---
name: task-mgmt-api
description: Build and extend the Task Management System REST API — an ASP.NET Core Web API on .NET 8 structured with Domain-Driven Design (Domain / Application / Infrastructure / Api layers), SQL Server, and Entity Framework Core code-first migrations. Use this skill whenever working in this repository: scaffolding or restructuring the solution, adding an entity/aggregate, value object, endpoint, controller, use case, repository, DbContext configuration, or EF migration, wiring up validation or error handling, or answering "where should this code go?" questions. Trigger even if the user just says "add a feature", "add an endpoint for X", "create a table", or "update the database", since all of those must follow the layering rules here.
---

# Task Management API — .NET 8 · DDD · SQL Server · EF Core Code-First

This skill describes how the Task Management System API is structured and how to grow it. The goal is a codebase that stays clean while small and can scale into a large application (more aggregates, more bounded contexts, more developers) without rewrites. Most of the rules below exist to keep **business logic in the domain** and **technology at the edges**, so either side can change without breaking the other.

For full code templates (base classes, entity, EF configuration, handler, controller, Program.cs), read `references/code-templates.md` when you need to write those pieces.

---

## 1. Tech stack and versions

| Concern | Choice | Notes |
|---|---|---|
| Runtime | .NET 8 (`net8.0`) | Pin the SDK with `global.json` because .NET 9+ SDKs may also be installed. |
| Web | ASP.NET Core Web API with **controllers** | Swagger via Swashbuckle in Development. |
| Database | Local SQL Server 2022 default instance (`localhost`), **Windows authentication** | Connection string `ConnectionStrings:Default` in `appsettings.json`. LocalDB is no longer used. |
| ORM | EF Core **8.0.x**, code-first | `Microsoft.EntityFrameworkCore.SqlServer`, `.Design`, `.Tools`. Keep all EF packages on the same 8.0.x patch. |
| Validation | FluentValidation 12.x (supports `net8.0`) | Validators live in Application. |
| Mapping | Manual mapping (extension methods / static `FromDomain`) | Avoid AutoMapper. Explicit mapping is easier to follow and AutoMapper is now commercially licensed. |
| Mediator | No MediatR. Use simple handler interfaces (see §4). | Keeps things dependency-free. MediatR is also commercially licensed now. |
| Tests | **NUnit 4.4.0** (constraint model `Assert.That`), NetArchTest for layer rules | **Read `Specs.md` before writing any test.** Packages come from `tests/Directory.Build.props`. |
| Auth | JWT bearer (HS256, 60 min, no refresh), ASP.NET Core Identity `PasswordHasher` | See §6a. |

**This project targets .NET 8 only. That is a fixed project decision.** Every project uses `net8.0`. Every `Microsoft.*` / EF Core package uses an **8.0.x** version, and the `dotnet-ef` tool is 8.0.x. Don't upgrade to .NET 9/10 or EF Core 9/10, and don't accept a package version that needs a newer framework, even if a newer SDK is installed or a tool suggests it. When adding a package with `dotnet add package`, always pass `--version` explicitly. Otherwise NuGet picks the latest major version, which targets a newer framework and fails restore or silently drifts. Third-party packages (FluentValidation, Swashbuckle, NetArchTest, xUnit) must use versions that support `net8.0`.

Pinned versions currently in use (check the csproj files before adding a package, and match them):

| Package | Version | Project |
|---|---|---|
| Microsoft.EntityFrameworkCore.SqlServer | 8.0.31 | Infrastructure |
| Microsoft.EntityFrameworkCore.Design | 8.0.31 | Api |
| Microsoft.Extensions.Configuration.Abstractions | 8.0.0 | Infrastructure |
| Microsoft.Extensions.DependencyInjection.Abstractions | 8.0.2 | Application |
| FluentValidation (+ .DependencyInjectionExtensions) | 12.2.0 | Application |
| Swashbuckle.AspNetCore | 6.6.2 | Api |
| NetArchTest.Rules | 1.3.2 | ArchitectureTests |
| Microsoft.AspNetCore.Mvc.Testing | 8.0.31 | Api.IntegrationTests |
| Microsoft.AspNetCore.Authentication.JwtBearer | 8.0.31 | Api |
| Microsoft.Extensions.Identity.Core | 8.0.31 | Infrastructure |
| Microsoft.IdentityModel.JsonWebTokens | 7.7.3 (matches JwtBearer 8.0.31's IdentityModel) | Infrastructure |
| Microsoft.Extensions.Options.ConfigurationExtensions | 8.0.0 | Infrastructure |
| NUnit / NUnit3TestAdapter / NUnit.Analyzers | 4.4.0 / 5.2.0 / 4.15.0 | `tests/Directory.Build.props` (**NUnit stays on 4.4.x**: 4.5+ is ambiguous under C# 12) |
| Microsoft.NET.Test.Sdk / coverlet.collector | 17.14.1 / 6.0.4 | `tests/Directory.Build.props` |
| dotnet-reportgenerator-globaltool (local tool) | 5.5.11 | coverage reports |
| dotnet-ef (local tool, `.config/dotnet-tools.json`) | 8.0.31 | repo |

To find the newest 8.0.x patch of a Microsoft package, query `https://api.nuget.org/v3-flatcontainer/<id-lowercase>/index.json` and filter for `^8\.0\.\d+$`. When bumping a patch, bump all EF Core packages and the `dotnet-ef` tool together.

---

## 2. Solution layout

```
Api/                                   (repo root)
├── global.json                        pins .NET 8 SDK
├── Directory.Build.props              shared: net8.0, Nullable, ImplicitUsings, warnings
├── TaskManagement.sln
├── src/
│   ├── TaskManagement.Domain/         pure C#, NO package references to EF/ASP.NET
│   │   ├── Common/                    Entity, AggregateRoot, ValueObject, IDomainEvent, DomainException
│   │   └── Tasks/                     one folder per aggregate
│   │       ├── TaskItem.cs            aggregate root
│   │       ├── TaskItemId.cs          strongly-typed id
│   │       ├── TaskItemStatus.cs      enum
│   │       ├── TaskPriority.cs        enum
│   │       ├── Events/                TaskItemCreated, TaskItemCompleted …
│   │       └── ITaskItemRepository.cs
│   ├── TaskManagement.Application/    use cases; depends on Domain only
│   │   ├── Abstractions/              IUnitOfWork, ICommandHandler, IQueryHandler, Result, Error
│   │   ├── Tasks/
│   │   │   ├── CreateTask/            CreateTaskCommand, Handler, Validator
│   │   │   ├── UpdateTask/
│   │   │   ├── DeleteTask/            soft delete
│   │   │   ├── GetTaskById/           Query, Handler
│   │   │   ├── ListTasks/             status + priority filters, paging
│   │   │   ├── GetTaskSummary/
│   │   │   └── TaskDto.cs
│   │   └── DependencyInjection.cs     AddApplication()
│   ├── TaskManagement.Infrastructure/ EF Core, SQL Server, external services
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── Configurations/        IEntityTypeConfiguration<T> per aggregate
│   │   │   ├── Repositories/
│   │   │   └── Migrations/
│   │   └── DependencyInjection.cs     AddInfrastructure(configuration)
│   └── TaskManagement.Api/            composition root + HTTP
│       ├── Controllers/               TasksController …
│       ├── Contracts/                 request/response models (HTTP shape)
│       ├── Infrastructure/            GlobalExceptionHandler, ResultExtensions
│       ├── Program.cs
│       └── appsettings*.json
├── Specs.md                           testing specification (NUnit conventions, coverage); read before writing tests
├── coverage.runsettings               coverlet settings
├── scripts/Set-DevAuthUser.ps1        local test login → user-secrets (hash only)
└── tests/                             all NUnit; shared packages in tests/Directory.Build.props
    ├── TaskManagement.Domain.UnitTests/
    ├── TaskManagement.Application.UnitTests/      Fakes.cs + handler/validator/login tests
    ├── TaskManagement.Infrastructure.UnitTests/   authenticator, JWT, embedded SQL
    ├── TaskManagement.ArchitectureTests/
    └── TaskManagement.Api.IntegrationTests/       TestApp (one start, one login), real DB, auth/CORS/error-shape tests
```

Group code **by feature/aggregate** (`Tasks/`, later `Projects/`, `Users/`) inside each layer, not by technical type. When the app becomes large, each feature folder can be lifted into its own module or bounded context with minimal churn.

### Dependency rule (the most important rule)

```
Api ──► Application ──► Domain
 │                        ▲
 └──► Infrastructure ─────┘   (Infrastructure ──► Application too, to implement its interfaces)
```

- **Domain** references nothing (no EF, no ASP.NET, no FluentValidation).
- **Application** references Domain only. It defines interfaces (`IUnitOfWork`, `IEmailSender`, …) that Infrastructure implements.
- **Infrastructure** references Application (and through it Domain). It holds all EF Core / SQL Server code.
- **Api** references Application and Infrastructure. Infrastructure is referenced only so `Program.cs` can call `AddInfrastructure()`. Controllers never use `AppDbContext` or repositories directly; they call Application handlers.

If you feel the need to break this rule (for example, putting an `[Required]` attribute on a domain entity or injecting `AppDbContext` into a controller), that is a sign the code belongs in a different layer. The architecture tests in `tests/TaskManagement.ArchitectureTests` enforce it, so keep them passing.

---

## 3. Domain layer rules

The domain model is where the business rules live. Keep it rich, not anemic:

- **Aggregates own their invariants.** Properties have `private set`. State changes happen through aggregate methods (`task.Update(...)`, `task.Delete(now)`), not by setting properties from outside. Creation goes through a static factory (`TaskItem.Create(...)`) that validates inputs.
- **Throw `DomainException`** (or return a domain error) when an invariant is violated, e.g. modifying a deleted task. Application/Api translate it to a 400/409 response.
- **Strongly-typed IDs** (`readonly record struct TaskItemId(Guid Value)`) stop you from passing a `ProjectId` where a `TaskItemId` is expected. Generate them in the domain (`TaskItemId.New()`), not in the database.
- **Value objects** (`ValueObject` base or `record`) for concepts with no identity (e.g. `DateRange`, `Email`). Map them with EF owned types or `ComplexProperty`.
- **Domain events** (`TaskItemCompleted`) are raised inside aggregate methods and dispatched after `SaveChanges` succeeds. Use them for side effects (notifications, audit) so aggregates don't depend on services.
- **Audit fields and soft delete (project standard):** aggregates have `CreatedDate` (set in `Create`), `ModifiedDate` (set by every mutating method), `IsDeleted`, and `DeletedDate`. Rows are never removed from the database. Deleting means calling `aggregate.Delete(nowUtc)`, and mutating methods call `EnsureNotDeleted()` first. Use these field names (not `CreatedAtUtc`) because they are part of the agreed spec; the values are still UTC.
- **Time is injected.** Pass `DateTime nowUtc` into domain methods. Application gets it from .NET 8's built-in `TimeProvider`. Store all times in UTC.
- **A private parameterless constructor** is allowed for EF Core. It's the only concession the domain makes to persistence.
- **Repository interfaces** (`ITaskItemRepository`) live next to their aggregate in Domain, one per aggregate root only (never for child entities).

**Naming gotcha:** don't name the aggregate `Task` or the enum `TaskStatus`. `ImplicitUsings` imports `System.Threading.Tasks`, so these collide with `Task`/`TaskStatus` everywhere. Use `TaskItem`, `TaskItemStatus`.

---

## 4. Application layer rules

The Application layer orchestrates use cases. It loads aggregates, calls domain methods, and saves. It contains no business rules of its own.

- **One folder per use case**: `CreateTask/` holds `CreateTaskCommand` (a record), `CreateTaskCommandHandler`, and `CreateTaskCommandValidator`.
- **Commands** change state and return `Result` / `Result<TId>`. **Queries** read state and return `Result<TDto>`. This lightweight CQRS split lets queries later bypass the domain model (e.g. `AsNoTracking` projections or Dapper) for performance without touching commands.
- Handlers implement `ICommandHandler<TCommand, TResult>` / `IQueryHandler<TQuery, TResult>` and are registered in `AddApplication()`.
- **Validation** of input shape (required, max length, ranges) uses FluentValidation inside the handler before touching the domain. Business invariants stay in the domain.
  - Command/query fields that come from the client are **nullable, and enums are `string?`**. That way a missing or invalid value reaches the validator and becomes a field-level `Validation.Failed` error, instead of failing during JSON binding with a framework message. Validate enums with `.MustBeEnumName<T, TEnum>()` (member names only, case-insensitive, never numbers), then convert with `EnumNames.Parse<TEnum>()` in the handler.
  - Put field rules shared by several commands in `TaskValidationRules`-style extension methods so Create and Update can't drift apart.
  - Messages are plain sentences with the C# property name: `"Title is required."`, `"Status must be one of: ToDo, InProgress, Done."`. `errors` keys are C# property names (`Title`), not camelCase.
  - `validation.ToError()` produces `Error.Validation(...)` with per-field `ValidationErrors`. Never join messages into one string.
- **Error codes** come from `Application/Abstractions/ErrorCodes.cs`, in `Area.Reason` format (`Task.NotFound`). Add new codes there; never use inline string literals, and never rename an existing code, because clients switch on them. Feature errors (`TaskErrors`) use these constants.
- **Result pattern** for expected failures (not found, validation, conflict): return `Result.Failure(Error.NotFound(...))` instead of throwing. Exceptions are for truly exceptional situations.
- **DTOs** (`TaskDto`) are what Application returns. Never return domain entities out of Application.
- **One `IUnitOfWork.SaveChangesAsync`** call per command, at the end of the handler, so each use case is one transaction.
- Always accept and pass through a `CancellationToken`.

---

## 5. Infrastructure layer rules (EF Core code-first + SQL Server)

- `AppDbContext : DbContext, IUnitOfWork` exposes `DbSet<TaskItem>` and calls `modelBuilder.ApplyConfigurationsFromAssembly(...)`.
- **All mapping is Fluent API** in `Persistence/Configurations/<Entity>Configuration.cs` (`IEntityTypeConfiguration<T>`). Don't use data annotations, since they would leak EF into the domain.
- In every configuration: set the table name, key, the strongly-typed ID conversion, `HasMaxLength` on every string (otherwise SQL Server gets `nvarchar(max)`), `IsRequired` where needed, enum conversions (`HasConversion<string>()` with max length so the DB stays readable), and indexes for columns you filter on.
- **UTC round-tripping:** SQL Server `datetime2` does not store `DateTimeKind`, so EF reads values back as `Unspecified` and the JSON loses its `Z` suffix (clients then treat it as local time). `AppDbContext.ConfigureConventions` registers `UtcDateTimeConverter` for all `DateTime`/`DateTime?` properties. Keep it, and don't add per-property date conversions.
- Add a `rowversion` concurrency token (`byte[] RowVersion`, `.IsRowVersion()`) to aggregates that multiple users can edit concurrently.
- Repositories implement the domain interfaces and expose only what the aggregate needs (`GetByIdAsync`, `Add`). There is no `Remove`, because deletes are soft. Every soft-deletable entity's configuration has `HasQueryFilter(x => !x.IsDeleted)`, so deleted rows are invisible everywhere. Call `IgnoreQueryFilters()` only on purpose, for audit or restore features. Don't build a generic `IRepository<T>` that exposes `IQueryable`, because that lets query logic leak everywhere.
- Register with `services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()))`.
- Domain events are dispatched from `AppDbContext.SaveChangesAsync` after the base save succeeds.

### Migrations workflow

Migrations live in Infrastructure; the Api project is the startup project (it has the connection string and must reference `Microsoft.EntityFrameworkCore.Design`).

The EF CLI is pinned to 8.0.x as a **repo-local tool** (`.config/dotnet-tools.json`). A developer's global `dotnet-ef` may be 9.x or 10.x, and the local manifest takes precedence inside the repo. On a fresh clone run `dotnet tool restore` once.

```powershell
# check whether the model has changed since the last migration
dotnet ef migrations has-pending-model-changes `
  --project src/TaskManagement.Infrastructure `
  --startup-project src/TaskManagement.Api

# after changing an entity or configuration
dotnet ef migrations add <DescriptiveName> `
  --project src/TaskManagement.Infrastructure `
  --startup-project src/TaskManagement.Api `
  --output-dir Persistence/Migrations

# apply to the database
dotnet ef database update `
  --project src/TaskManagement.Infrastructure `
  --startup-project src/TaskManagement.Api
```

- Name migrations after the change: `AddTaskPriority`, `CreateProjectsTable`, not `Update1`.
- Always read the generated migration before applying it. Watch for unintended column drops/renames (EF sometimes emits drop+add instead of a rename, which loses data).
- Never edit a migration that has already been applied to a shared database; add a new one instead.

### Seed data (agreed in task.md T7: every environment, via migration)

Sample/reference data is delivered by **migrations that run `.sql` files of INSERT statements**, so it is created together with the tables in every environment (`database update`, API startup `MigrateAsync`, and `migrations script` for production).
- Put the SQL in `src/TaskManagement.Infrastructure/Persistence/Migrations/Sql/<Name>.sql` (embedded via `<EmbeddedResource Include="Persistence\**\*.sql" />`). Create an empty migration with `dotnet ef migrations add <Name>` and call `migrationBuilder.Sql(SqlScripts.Read("<Name>.sql"))` in `Up()`. `Down()` runs a matching `Remove<Name>.sql`.
- Seed rows use **fixed Ids in a recognisable range** (current seed: `5EED0000-0000-0000-0000-0000000000NN`) and `WHERE NOT EXISTS`, so the script is idempotent and `Down()` removes only seed rows, never user data.
- Seed values must obey the same rules as the API: valid enum names, max lengths, and consistent audit fields (`CompletedAtUtc` only for Done, `ModifiedDate` for touched rows, `DeletedDate` only for deleted rows). Make dates relative to `SYSUTCDATETIME()` so the data always looks current.
- No `GO` and no `BEGIN TRAN` in seed files: EF runs each migration as a single batch inside its own transaction.
- To change seed data later, add a **new** migration (e.g. `UpdateSampleTasks`) instead of editing `SeedSampleTasks.sql`, because the old file has already run in other environments.
- `SeedDataTests` asserts the seed exists. Integration tests must still never depend on exact seed counts for other assertions; they use their own marker rows or before/after deltas.
- In Development, the app may auto-apply migrations at startup (`db.Database.MigrateAsync()` guarded by `IsDevelopment()`). For production, generate an idempotent script (`dotnet ef migrations script --idempotent`) or a migration bundle. Don't auto-migrate in production.

### Connection strings

The database is `TaskManagementDb` on the **local SQL Server default instance**, using **Windows authentication** (agreed in task.md T6). The connection string is defined **once**, in `src/TaskManagement.Api/appsettings.json`; don't add a second copy to `appsettings.Development.json`.
```json
"ConnectionStrings": {
  "Default": "Server=localhost;Database=TaskManagementDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;Application Name=TaskManagement.Api"
}
```
- No `User Id` / `Password`: the API and the tests connect as the current Windows user.
- `TrustServerCertificate=True` is there only because the local instance uses a self-signed certificate (Microsoft.Data.SqlClient encrypts by default). Never carry it over to a shared or production server.
- Other environments override it with the environment variable `ConnectionStrings__Default`. Never commit SQL credentials; if a server needs SQL authentication, use `dotnet user-secrets` locally or a vault.
- Create or upgrade the database only through migrations: `dotnet ef database update` with `--startup-project src/TaskManagement.Api` (see above). Never use hand-written DDL.

---

## 6. Api layer rules (RESTful design)

- **Thin controllers**: map request → command/query, call handler, map `Result` → HTTP response. No business logic, no EF.
- Route convention (agreed spec): **`api/<plural-resource>`** in kebab-case, with **no version segment** (`api/tasks`, `api/tasks/{id:guid}`). Don't reintroduce `api/v1`. If versioning is ever needed, discuss it with the user first.
- Always constrain id parameters (`{id:guid}`) so literal sub-routes like `summary` don't collide with them.
- HTTP verbs and status codes (current Tasks API):

| Action | Verb + route | Success | Errors |
|---|---|---|---|
| List (paged; optional `status`, `priority`) | `GET /api/tasks?status=ToDo&priority=High&page=1&pageSize=20` | 200 with paged envelope | 400 |
| Summary | `GET /api/tasks/summary` | 200 | — |
| Get one | `GET /api/tasks/{id}` | 200 | 404 (missing or deleted) |
| Create | `POST /api/tasks` | 201 + `Location` header (`CreatedAtAction`) | 400 |
| Full update (including `status`) | `PUT /api/tasks/{id}` | 204 | 400, 404, 409 |
| Soft delete | `DELETE /api/tasks/{id}` | 204 | 404 |

- **Status changes go through `PUT`** (agreed spec). Don't add action endpoints like `/start`, `/complete`, `/reopen`. Status rules belong in the domain's `Update` method.
- `DELETE` is always a **soft delete**. Document that on the action, and never hard-delete through the API.
- Only add endpoints that a work item in `task.md` asks for.
- **One error format for every response** (RFC 7807 ProblemDetails + `code` + `traceId`, content type `application/problem+json`): `type`, `title` (reason phrase), `status`, `detail`, `instance`, `code`, `traceId`, plus `errors` for validation failures. **`Api/Infrastructure/ApiProblems.cs` is the only place that builds error bodies.** Everything routes through it:
  - handler `Result` failures → `result.ToProblem(this)` (ErrorType → 400/404/409);
  - model-binding errors → `ModelStateProblems` (registered as `InvalidModelStateResponseFactory`);
  - exceptions → `GlobalExceptionHandler` (`DomainException` 400 with its `Code`, `DbUpdateConcurrencyException` 409, `DbUpdateException` 500 `Database.Error`, anything else 500 `Server.Error`);
  - empty framework responses (404 unknown route, 405, 415) → `UseStatusCodePages()` + `CustomizeProblemDetails` → `ApiProblems.Enrich`.
  Never construct `ProblemDetails` or call `Problem()` / `BadRequest(...)` directly in a controller.
- 5xx bodies contain only a generic message and the `traceId`, never exception text, type names, SQL, or stack traces. The full exception is logged at Error with the same `traceId`; 4xx are logged at Warning without stack traces.
- Don't put `[Produces("application/json")]` on controllers. It overwrites `application/problem+json` on error responses. Success responses default to `application/json` anyway.
- Every new error scenario needs an integration test in `tests/TaskManagement.Api.IntegrationTests` that asserts status, `code`, and the full shape (see `ProblemAssert.IsProblem`). Those tests run against the real local database (agreed). Simulate failures (conflicts, DB errors, crashes) by replacing a handler with `ConfigureTestServices` instead of breaking the database.
- NUnit runs tests sequentially by default. **Never add `[Parallelizable]` to integration tests**, because database tests would change each other's counts. Use `TestApp.AuthorizedClient()` / `TestApp.AnonymousClient()` (one app start and one login per run); see Specs.md §5.
- Integration tests that **write** (e.g. `TaskApiTests`) go through `TestDatabase`: every row gets the per-test `Marker` in its title, assertions only look at the test's own rows (other data may exist), and `Dispose` deletes them, so the table is left as it was. Match the marker with `LEFT(Title, LEN(@Marker)) = @Marker`, not `LIKE`, because `[` is a LIKE wildcard. Send `DateTime` parameters as `SqlDbType.DateTime2`, because `AddWithValue` infers legacy `datetime` and loses precision.
- When a feature adds or changes stored fields, add a round-trip test proving the API returns exactly what is in SQL and stores exactly what it is sent.
- Request/response contracts live in `Api/Contracts` and are separate from Application commands, so the HTTP shape can evolve independently.
- Lists are always paginated (cap `pageSize`, e.g. at 100). Unbounded lists are a common scaling problem.
- Annotate actions with `[ProducesResponseType]` so Swagger documents the responses.

### 6a. Authentication and authorization (task.md T8, D15–D20)

- **Every endpoint requires a JWT** through the authorization **fallback policy** (`AuthSetup.AddApiAuth`). New controllers are protected automatically; don't add `[Authorize]` everywhere, and **only** mark an action `[AllowAnonymous]` when the user explicitly asks (today only `POST /api/auth/login`). Clients log in once and reuse the token for 60 minutes; there is no refresh token.
- Login flow: `AuthController` → `LoginCommand` (validator: email ≤ 256, password ≤ 128) → `IUserAuthenticator` → `IAccessTokenService`. Unknown user and wrong password return the **identical** 401 `Auth.InvalidCredentials`.
- **Credentials and secrets never go in the repository**: no usernames/passwords in code, config, docs, `.http` files, or tests (D16). Test users live in **dotnet user-secrets** (`scripts/Set-DevAuthUser.ps1 -Username … -Password …` stores only an Identity V3 password hash) and only work in Development (`AuthOptions.Users` is cleared in other environments). The JWT signing key comes from user-secrets or the `Jwt__SigningKey` environment variable; startup fails without it. Never log, echo, or return a password (records redact it in `ToString()`).
- Token validation: issuer `TaskManagement.Api`, audience `TaskManagement.Ui`, lifetime with 1 minute skew, HS256 only, `MapInboundClaims = false` (claims `sub`, `email`, `jti`). Issuing and validating share `JwtSigningKey.Create`.
- 401/403 from the framework go through `UseStatusCodePages` → `ApiProblems` (`Auth.Unauthorized` / `Auth.Forbidden`). Anonymous requests to unknown routes also get 401 (by design, because of the fallback policy).
- CORS: only `Cors:AllowedOrigins` (currently `https://localhost:5173`, the React dev server); methods GET/POST/PUT/DELETE; headers `Authorization`, `Content-Type`.
- Application stays free of auth technology: no JWT, Identity, or ASP.NET types (the architecture test enforces this).

---

## 7. Workflows

### A. Initial scaffold / restructuring the template project

**Status: done.** The layered solution (`TaskManagement.sln`, `src/`, `tests/`) exists, and the `InitialCreate` migration has been applied. The old template folder `Task_Management_System/` has been deleted. Use this workflow only when recreating the solution from scratch.

The original steps for moving from the default template (`Task_Management_System/` with `WeatherForecast`) to the layered structure:

1. Confirm with the user before deleting or moving the template project. Suggest moving it to `src/TaskManagement.Api` (or recreating it) rather than silently removing it.
2. Run the scaffold commands below from the repo root.
3. Add `Directory.Build.props` and `global.json` (see templates).
4. Add packages: EF Core SqlServer to Infrastructure; EF Core Design to Api; FluentValidation (+ `.DependencyInjectionExtensions`) to Application; NetArchTest.Rules to ArchitectureTests.
5. Create the Domain base classes, the `TaskItem` aggregate, the Application abstractions, `AppDbContext`, and `Program.cs` from `references/code-templates.md`.
6. Delete the WeatherForecast sample once the new controller works.
7. `dotnet build`, add the `InitialCreate` migration, `dotnet ef database update`, run, and verify in Swagger.

```powershell
dotnet new globaljson --sdk-version 8.0.425 --roll-forward latestFeature
dotnet new sln -n TaskManagement
dotnet new classlib -n TaskManagement.Domain          -o src/TaskManagement.Domain          -f net8.0
dotnet new classlib -n TaskManagement.Application     -o src/TaskManagement.Application     -f net8.0
dotnet new classlib -n TaskManagement.Infrastructure  -o src/TaskManagement.Infrastructure  -f net8.0
dotnet new webapi   -n TaskManagement.Api             -o src/TaskManagement.Api             -f net8.0 --use-controllers
# Test projects: plain class libraries; NUnit packages + global using come from tests/Directory.Build.props
dotnet new classlib -n TaskManagement.Domain.UnitTests      -o tests/TaskManagement.Domain.UnitTests      -f net8.0
dotnet new classlib -n TaskManagement.Application.UnitTests -o tests/TaskManagement.Application.UnitTests -f net8.0
dotnet new classlib -n TaskManagement.ArchitectureTests     -o tests/TaskManagement.ArchitectureTests     -f net8.0

Get-ChildItem -Recurse -Filter *.csproj src, tests | ForEach-Object { dotnet sln add $_.FullName }

dotnet add src/TaskManagement.Application    reference src/TaskManagement.Domain
dotnet add src/TaskManagement.Infrastructure reference src/TaskManagement.Application
dotnet add src/TaskManagement.Api            reference src/TaskManagement.Application src/TaskManagement.Infrastructure
dotnet add tests/TaskManagement.Domain.UnitTests      reference src/TaskManagement.Domain
dotnet add tests/TaskManagement.Application.UnitTests reference src/TaskManagement.Application
dotnet add tests/TaskManagement.ArchitectureTests     reference src/TaskManagement.Api
```

Then add the packages with the **explicit versions from the table in §1** (e.g. `dotnet add src/TaskManagement.Infrastructure package Microsoft.EntityFrameworkCore.SqlServer --version 8.0.31`). Pin the EF CLI locally with `dotnet new tool-manifest` followed by `dotnet tool install dotnet-ef --version 8.0.31`.

Use the latest installed 8.0.x SDK version in `global.json` (check with `dotnet --list-sdks`). Delete the auto-generated `Class1.cs` / `UnitTest1.cs` files and the WeatherForecast sample.

### B. Adding a new feature (e.g. "add Projects" or "tasks can be assigned to a user")

Work items are tracked in **`task.md`** at the repo root. Before starting, read the relevant item and any open decisions. Tick its checkboxes as you finish sub-tasks, and record new decisions in its "Decisions" table.

Work inside-out, layer by layer. This order means each layer compiles against something that already exists:

1. **Domain:** add or extend the aggregate, its ID, enums/value objects, invariants, domain events, and repository interface. Add domain unit tests for the new invariants.
2. **Application:** add one folder per use case (command/query + handler + validator), plus DTOs. Register handlers in `AddApplication()`.
3. **Infrastructure:** add or update the `IEntityTypeConfiguration`, the `DbSet`, and the repository implementation. Register it in `AddInfrastructure()`.
4. **Migration:** `dotnet ef migrations add <Name>`, review the generated code, `dotnet ef database update`. Afterwards, `has-pending-model-changes` should report no changes.
5. **Api:** add request/response contracts and controller actions following the REST table in §6.
6. **Verify:** `dotnet build` and `dotnet test` (architecture tests included), then exercise the endpoint via Swagger or `src/TaskManagement.Api/TaskManagement.Api.http`, and add the new requests to that file.

Before coding, decide whether the new concept is its **own aggregate** or **part of an existing one**. Rule of thumb: if it has its own lifecycle and is edited independently (a Project), it's an aggregate referenced **by ID** (`ProjectId` on `TaskItem`, not a `Project` navigation property). If it only exists inside its parent and must stay consistent with it (checklist items in a task), it's a child entity in the parent aggregate. Referencing other aggregates by ID keeps aggregates small and makes splitting into separate services possible later.

### C. Changing an existing entity

Change the domain method/property → update the EF configuration → add a migration → update DTOs/contracts → update tests. Consider existing rows: a new required column needs a default value or a data migration step.

---

## 8. Scaling notes (for when the app grows)

These are not needed on day one. Keep them in mind so early decisions don't block them:

- **Bounded contexts / modular monolith:** when a second domain area appears (e.g. Identity, Notifications), give it its own `Domain/Application/Infrastructure` set (or folder module) with its own `DbContext` and schema (`modelBuilder.HasDefaultSchema("tasks")`). Contexts communicate through integration events or application interfaces, never by sharing entities.
- **Read side:** heavy list/report endpoints can use dedicated query handlers with `AsNoTracking()` projections straight to DTOs, or raw SQL, without loading aggregates.
- **Raw SQL (no EF) pattern**, as used by `GET /api/tasks/summary`: define the interface in Application (`ITaskSummaryQuery`). Implement it in `Infrastructure/Persistence/Queries/` with **plain ADO.NET** (`SqlConnectionFactory` → `SqlCommand`, timeout, `CancellationToken`; no Dapper). Keep the SQL in an embedded `Persistence/Queries/Sql/<Name>.sql`, read via `SqlScripts.Read`. Raw SQL **bypasses the EF soft-delete query filter**, so every query must say `WHERE IsDeleted = 0` itself. Use parameters for every input, never string concatenation. `SqlException` maps to 500 `Database.Error`.
- **Cross-cutting concerns** (logging, validation, transactions) can later move into handler decorators.
- **Outbox pattern:** if domain events must reliably trigger external side effects (emails, message bus), persist them to an outbox table in the same transaction and publish them in a background worker.
- **Auth (done, see §6a):** if handlers later need the logged-in user, put it behind an `ICurrentUser` interface in Application (implemented in Api from `HttpContext.User`, claim `sub`). A real user store replaces `ConfiguredUserAuthenticator` behind `IUserAuthenticator` without API changes. Refresh tokens and logout are not implemented (D17).

---

## 9. Definition of done for any change

- Layer rule respected (architecture tests pass).
- Business rules live in the domain and are covered by domain unit tests.
- Strings have max lengths in EF configuration; a migration was added and reviewed if the model changed.
- Endpoints return correct status codes and ProblemDetails on failure; lists are paginated.
- `dotnet build` has no warnings introduced (NUnit analyzers included); `dotnet test` passes.
- New behaviour has **NUnit** tests for every positive and negative scenario (Specs.md §6), and unit-test line coverage stays **above 90%** (Specs.md §8).
- No credentials or secrets were added to the repository.
