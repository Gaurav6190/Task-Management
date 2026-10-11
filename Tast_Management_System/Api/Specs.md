# Testing Specification (NUnit)

How tests are written and run in the Task Management API. Applies to **every** test project. Decisions behind it: `task.md` D9, D16, D21 and T9.

---

## 1. Framework and packages

**NUnit is the only test framework** in this repo. Don't add xUnit or MSTest.

All test projects get their packages from **`tests/Directory.Build.props`**, so a test `.csproj` only lists project references (and extra packages such as NetArchTest or Mvc.Testing). Don't add NUnit package references to individual projects.

| Package | Version | Why |
|---|---|---|
| `NUnit` | **4.4.0** | Pinned. 4.5+/4.6 add `Action` overloads to `Assert.Multiple`/`Assert.Throws` that are **ambiguous under C# 12** (the .NET 8 SDK): every `Assert.Multiple(() => …)` fails with CS0121. Only upgrade together with a newer C# version. |
| `NUnit3TestAdapter` | 5.2.0 | Runs NUnit 4 tests in `dotnet test` / Visual Studio. |
| `NUnit.Analyzers` | 4.15.0 | Catches NUnit mistakes at build time. Warnings must be fixed; the build stays at 0 warnings. |
| `Microsoft.NET.Test.Sdk` | 17.14.1 | Test host. |
| `coverlet.collector` | 6.0.4 | Code coverage (section 8). |

`using NUnit.Framework;` is a global using (from the props file). Don't repeat it.

## 2. Test projects

| Project | Tests | Touches the DB? |
|---|---|---|
| `TaskManagement.Domain.UnitTests` | `TaskItem` rules, base classes (`Entity`, `ValueObject`, `TaskItemId`, `DomainException`) | No |
| `TaskManagement.Application.UnitTests` | handlers, validators, `Result`/`Error`, `EnumNames`, DI registrations, login use case (with fakes in `Fakes.cs`) | No |
| `TaskManagement.Infrastructure.UnitTests` | `ConfiguredUserAuthenticator`, `JwtAccessTokenService`, embedded SQL scripts | No |
| `TaskManagement.ArchitectureTests` | layer dependency rules (NetArchTest) | No |
| `TaskManagement.Api.IntegrationTests` | every HTTP endpoint end to end: auth, authorization, CORS, validation, error shapes, DB round trips, summary, seed | **Yes**, the real local SQL Server |

Rule of thumb: business rules and branches go in **unit tests** (fast, exhaustive). HTTP behaviour (status, `code`, response shape, auth) goes in **integration tests**.

## 3. Writing a test

```csharp
[TestFixture]
public class CreateTaskCommandHandlerTests
{
    private FakeTaskItemRepository _repository = null!;

    [SetUp]                                   // runs before EACH test: fresh state, no sharing
    public void SetUp() => _repository = new FakeTaskItemRepository();

    [Test]
    public async Task Create_invalid_command_returns_validation_error_without_saving()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new CreateTaskCommand("", null, "Urgent", null, null), CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.ValidationFailed));
            Assert.That(_repository.Items, Is.Empty);
        });
    }
}
```

- **Naming:** `Subject_condition_expected_result` in plain words, for example `Wrong_password_is_rejected_with_invalid_credentials`. The name is the specification, so no `Test1` and no `ShouldWork`.
- **One behaviour per test.** Several asserts are fine when they describe one outcome; wrap them in `Assert.Multiple`, so all failures are reported at once.
- **Arrange / Act / Assert**, separated by blank lines.
- Async tests return `async Task`, never `async void`.
- No test depends on another test or on execution order.

### Attributes used in this repo

| Attribute | Use |
|---|---|
| `[TestFixture]` | On every test class. |
| `[Test]` | A single test. |
| `[TestCase(...)]` | The same test with several inputs (e.g. every invalid enum value). Prefer this over copy-pasted tests. |
| `[ValueSource(nameof(X))]` + `[Values]` | Combinatorial tests: every endpoint × every bad-token kind in `AuthorizationTests`. |
| `[SetUp]` / `[TearDown]` | Per-test setup and cleanup (fakes, `TestDatabase`, `HttpClient`). |
| `[OneTimeSetUp]` / `[OneTimeTearDown]` | Expensive setup shared by a fixture. |
| `[SetUpFixture]` | `TestApp` in the integration tests: starts the API **once** and logs in **once** for the whole run. It must stay in the root namespace `TaskManagement.Api.IntegrationTests`, because it only applies to its namespace and the ones below it. |

### Assertions

Use the **constraint model** only: `Assert.That(actual, Is.EqualTo(expected))`. Don't use `ClassicAssert.AreEqual` and friends.

| Need | Write |
|---|---|
| equality / null / empty | `Is.EqualTo(x)`, `Is.Null`, `Is.Empty`, `Has.Count.EqualTo(n)` |
| collections | `Is.EquivalentTo(...)` (any order), `Is.EqualTo(...)` (same order), `Does.Contain(x)`, `Is.All.Zero` |
| strings | `Does.Contain`, `Does.StartWith`, `Does.EndWith`, `Does.Not.Contain` |
| ranges / dates | `Is.InRange(from, to)` |
| exceptions | `var ex = Assert.Throws<DomainException>(() => …);` then assert on `ex!.Message` |
| several asserts, sync | `Assert.Multiple(() => { … });` |
| several asserts with `await` inside | `await Assert.MultipleAsync(async () => { … });`. Never `Assert.Multiple(async () => …)`: that compiles to `async void`, and failures get lost. |

**Gotchas (each one hit us in practice):**
- **NUnit2009 analyzer:** don't write the same expression on both sides (`Is.EqualTo(new Person(id))` vs `new Person(id)`). Put each side in its own variable.
- **Nullable flow:** the compiler treats `x.Equals(null)` as a null check on `x`, so any later `x.…` warns CS8602. Do the other calls first.
- **Assert messages:** pass a reason as the last argument when the expectation isn't obvious, e.g. `Assert.That(row["Priority"], Is.EqualTo("High"), "stored by canonical name")`.

## 4. Unit test rules

- No database, no network, no file system, no clock. Pass `TimeProvider` / fixed dates explicitly.
- Use the hand-written **fakes** in `TaskManagement.Application.UnitTests/Fakes.cs` (repository, unit of work, queries, authenticator, token service). No mocking library is needed. Fakes record their calls, so tests can assert "the repository was never called".
- The fake repository mirrors the EF soft-delete filter (deleted tasks are not found). Keep fakes behaving like the real thing.
- Every validator rule needs a positive **and** a negative case, including boundaries: exactly at the max length passes, max + 1 fails.

## 5. Integration test rules (real database)

The integration tests run the **real API against the real local SQL Server** (`localhost/TaskManagementDb`, Windows authentication), as agreed in D9.

- **Start once, log in once:** use `TestApp.AuthorizedClient()` (shared token) or `TestApp.AnonymousClient()` (for 401 tests). Don't create your own `WebApplicationFactory`. For special setups, derive from the shared one: `TestApp.Factory.WithWebHostBuilder(…)` (e.g. replacing a handler with `ConfigureTestServices`, or `UseEnvironment("Production")`).
- **Never real credentials (D16):** `ApiFactory` creates a throwaway user (`it-user@integration.test`) with a **random password** and a **random JWT signing key** per run, in memory. Tests don't depend on anyone's user-secrets and contain no real passwords.
- **Own your rows:** every row a test inserts goes through `TestDatabase` and carries the per-test `Marker` (`[IT-xxxxxxxx]`) in its title. Dispose `TestDatabase` in `[TearDown]`, which deletes exactly those rows. After a run the table holds only the seed data plus whatever existed before.
- **Assert only on your own rows** when listing (seed data and other rows exist), or use **before/after deltas** (summary tests).
- **No parallelism:** NUnit runs tests sequentially unless `[Parallelizable]` is added. **Don't add it** to integration tests, because database tests would change each other's counts.
- **Simulate failures, don't cause them:** use `ConfigureTestServices` with a handler that throws (`DbUpdateConcurrencyException`, `SqlException`, …) instead of breaking the database.
- **Error responses:** always assert with `ProblemAssert.IsProblem(response, status, code)`, which checks status, `code`, `title`, `type`, `instance`, `traceId`, content type, and that no stack trace leaks.
- SQL in tests: match the marker with `LEFT(Title, LEN(@Marker)) = @Marker` (not `LIKE`, because `[` is a wildcard), and send dates as `SqlDbType.DateTime2` (already handled in `TestDatabase`).

## 6. Scenario checklist: what "covered" means

Every endpoint needs tests for **all positive and all negative scenarios**. The full matrix is in `task.md` T9; in short:

| Endpoint | Positive | Negative |
|---|---|---|
| `POST /api/auth/login` | valid login → token (60 min, claims); case-insensitive username; token works on all APIs | wrong password / unknown user (identical 401); missing/blank/invalid/too-long fields (400); bad JSON (400); wrong content type (415); test user rejected outside Development; password never in responses or logs |
| all `/api/tasks/**` | valid token accepted | missing, garbage, Basic, expired, wrong key, wrong issuer, wrong audience, unsigned → 401 |
| `GET /api/tasks` | filters (each, both, case-insensitive); paging; deleted excluded | invalid status/priority/page/pageSize (400) |
| `GET /api/tasks/{id}` | all fields equal the DB row | unknown / deleted (404); non-GUID (404 route) |
| `POST /api/tasks` | stored exactly; defaults; max-length boundaries | required / too long / invalid enum / numeric enum / past due date (400) |
| `PUT /api/tasks/{id}` | DB row updated; Done ↔ ToDo completion date | unknown / deleted (404); invalid status (400); due date moved to past (400 domain rule); conflict (409) |
| `DELETE /api/tasks/{id}` | soft delete (row kept, hidden everywhere) | unknown / already deleted (404) |
| `GET /api/tasks/summary` | equals direct SQL `GROUP BY`; 12 cells; totals; deltas | DB failure (500); unknown values skipped |

When you add or change an endpoint, add its rows here and in `task.md`, plus the tests.

## 7. Running tests

```powershell
dotnet test                                                  # everything (needs local SQL Server running)
dotnet test tests/TaskManagement.Application.UnitTests       # one project
dotnet test --filter "FullyQualifiedName~AuthTests"          # one fixture
dotnet test --filter "Name~Wrong_password"                   # tests by name
```
Prerequisites for integration tests: SQL Server running on `localhost` with your Windows login, and migrations applied (the API applies them at startup in Development). No user-secrets are required.

## 8. Code coverage

**Target (D21): unit-test line coverage above 90%.** Current (2026-10-11):

| Scope | Line coverage |
|---|---|
| Unit tests only, Domain + Application | **100%** |
| All tests, all production assemblies | **97.3%** (Domain 100%, Application 100%, Infrastructure 99.4%, Api 93%) |

Generate the report (the ReportGenerator tool is pinned in `.config/dotnet-tools.json`):
```powershell
dotnet tool restore
dotnet test --settings coverage.runsettings --results-directory ./TestResults
dotnet reportgenerator "-reports:TestResults/**/coverage.cobertura.xml" "-targetdir:TestResults/coverage" "-reporttypes:Html;TextSummary"
# open TestResults/coverage/index.html
```
- `coverage.runsettings` measures only production assemblies and excludes EF migration files.
- **Don't exclude `CompilerGeneratedAttribute`.** Async methods compile into generated state machines, so excluding it silently removes every async method and inflates the number (this happened once).
- New code must not lower the unit-test coverage below 90%. Untested defensive branches are listed in `task.md` T9.
