# Code Templates

Templates for the Task Management API. Where a section points to a real file, that file is canonical; read it instead of copying an old snippet. Copy and adapt them, keeping names and namespaces consistent (`TaskManagement.<Layer>.<Feature>`).

## Contents
1. Solution-level files (global.json, Directory.Build.props)
2. Domain: base classes
3. Domain: the Task aggregate (pointer to real code)
4. Application: abstractions (Result, Error, handlers, IUnitOfWork)
5. Application: use cases (pointer to real code)
6. Application: DependencyInjection
7. Infrastructure: AppDbContext, configuration, repository, DI
8. Api: controller, contracts, ResultExtensions, GlobalExceptionHandler, Program.cs
9. Tests: domain test + architecture test

---

## 1. Solution-level files

**global.json** (use the newest installed 8.0.x SDK):
```json
{
  "sdk": { "version": "8.0.425", "rollForward": "latestFeature" }
}
```

**Directory.Build.props** (repo root; once this exists, remove the duplicated properties from each csproj):
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>12</LangVersion>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <WarningsAsErrors>nullable</WarningsAsErrors>
  </PropertyGroup>
</Project>
```

---

## 2. Domain: base classes (`src/TaskManagement.Domain/Common/`)

```csharp
namespace TaskManagement.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
}

public abstract class Entity<TId> : IEquatable<Entity<TId>> where TId : notnull
{
    public TId Id { get; protected init; } = default!;

    public bool Equals(Entity<TId>? other) =>
        other is not null && other.GetType() == GetType() && EqualityComparer<TId>.Default.Equals(Id, other.Id);

    public override bool Equals(object? obj) => obj is Entity<TId> e && Equals(e);
    public override int GetHashCode() => EqualityComparer<TId>.Default.GetHashCode(Id);
}

public abstract class AggregateRoot<TId> : Entity<TId>, IHasDomainEvents where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}

public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}

public abstract class ValueObject : IEquatable<ValueObject>
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public bool Equals(ValueObject? other) =>
        other is not null && other.GetType() == GetType() &&
        GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());

    public override bool Equals(object? obj) => obj is ValueObject v && Equals(v);
    public override int GetHashCode() =>
        GetEqualityComponents().Aggregate(0, (hash, c) => HashCode.Combine(hash, c));
}

public sealed class DomainException(string message) : Exception(message);
```

Simple value objects can be `sealed record`s instead of inheriting `ValueObject`.

---

## 3. Domain: the Task aggregate (real code is canonical)

The Tasks feature is implemented. Read the real files instead of a template, and model new aggregates on them:

- `src/TaskManagement.Domain/Tasks/TaskItem.cs`: aggregate with `Create` factory, `Update` (including status), soft `Delete`, invariants via `DomainException`, `CreatedDate`/`ModifiedDate`/`IsDeleted`/`DeletedDate`, and `RowVersion`.
- `TaskItemId.cs` (strongly-typed id), `TaskItemStatus.cs` (`ToDo, InProgress, Done`), `TaskPriority.cs` (`Low, Medium, High, Critical`).
- `Events/TaskItemEvents.cs`: `TaskItemCreated`, `TaskItemCompleted`, `TaskItemDeleted`.
- `ITaskItemRepository.cs`: `GetByIdAsync` + `Add` only (no `Remove`, because deletes are soft).

Checklist for a new aggregate: private setters, private EF constructor, static `Create`, `ModifiedDate` set in every mutating method, `Delete(nowUtc)` that soft-deletes, and `EnsureNotDeleted()` guarding mutations.

---
## 4. Application: abstractions (`src/TaskManagement.Application/Abstractions/`)

**Result / Error / validation / error codes:** see the real files in `src/TaskManagement.Application/Abstractions/`: `Result.cs` (`Result`, `Result<T>`, `Error` with `ValidationErrors`), `ErrorCodes.cs` (the `Area.Reason` catalogue), and `ValidationExtensions.cs` (`ToError()`, `MustBeEnumName<T, TEnum>()`, `EnumNames`).

```csharp
namespace TaskManagement.Application.Abstractions;

public interface ICommandHandler<in TCommand>
{
    Task<Result> Handle(TCommand command, CancellationToken ct);
}

public interface ICommandHandler<in TCommand, TResponse>
{
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken ct);
}

public interface IQueryHandler<in TQuery, TResponse>
{
    Task<Result<TResponse>> Handle(TQuery query, CancellationToken ct);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
```


---

## 5. Application: use cases (real code is canonical)

See `src/TaskManagement.Application/Tasks/`. One folder per use case:

| Folder | Kind | Notes |
|---|---|---|
| `CreateTask/` | command → `Guid` | validator + handler; uses `TimeProvider` |
| `UpdateTask/` | command | full update including `Status` |
| `DeleteTask/` | command | soft delete via `task.Delete(now)` |
| `GetTaskById/` | query → `TaskDto` | `TaskErrors.NotFound` when missing or deleted |
| `ListTasks/` | query → `PagedResult<TaskDto>` | optional `status` + `priority` filters, `pageSize` ≤ 100, via `ITaskQueries` |
| `GetTaskSummary/` | query → `TaskSummaryDto` | placeholder; contents defined later |

Shared: `TaskDto.cs` (manual `FromDomain` mapping), `TaskErrors.cs`, `ITaskQueries.cs` (read-side interface implemented in Infrastructure).

---
## 6. Application: DependencyInjection

```csharp
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Tasks;
using TaskManagement.Application.Tasks.CreateTask;
using TaskManagement.Application.Tasks.GetTaskById;

namespace TaskManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddSingleton(TimeProvider.System);

        // Register every handler explicitly; add new use cases here.
        services.AddScoped<ICommandHandler<CreateTaskCommand, Guid>, CreateTaskCommandHandler>();
        services.AddScoped<IQueryHandler<GetTaskByIdQuery, TaskDto>, GetTaskByIdQueryHandler>();

        return services;
    }
}
```

Packages: `FluentValidation` + `FluentValidation.DependencyInjectionExtensions` (12.2.0), `Microsoft.Extensions.DependencyInjection.Abstractions` (8.0.2).
Once the handler count grows (roughly 20+), replace explicit registration with assembly scanning (e.g. Scrutor).

---

## 7. Infrastructure (`src/TaskManagement.Infrastructure/`)

```csharp
// Persistence/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Abstractions;
using TaskManagement.Domain.Common;
using TaskManagement.Domain.Tasks;
using TaskManagement.Infrastructure.Persistence.Converters;

namespace TaskManagement.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, IDomainEventDispatcher dispatcher)
    : DbContext(options), IUnitOfWork
{
    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Applies to DateTime and DateTime? properties.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var aggregates = ChangeTracker.Entries<IHasDomainEvents>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();

        var events = aggregates.SelectMany(a => a.DomainEvents).ToList();
        aggregates.ForEach(a => a.ClearDomainEvents());

        var result = await base.SaveChangesAsync(ct);
        await dispatcher.DispatchAsync(events, ct);
        return result;
    }
}
```

```csharp
// Persistence/Converters/UtcDateTimeConverter.cs
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TaskManagement.Infrastructure.Persistence.Converters;

/// SQL Server datetime2 does not store DateTimeKind, so values come back as Unspecified.
/// All times in this system are UTC; mark them as such on read so JSON serializes them with "Z".
internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : v,
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
```

```csharp
// Application/Abstractions/IDomainEventDispatcher.cs (+ handler contract)
using TaskManagement.Domain.Common;

namespace TaskManagement.Application.Abstractions;

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task Handle(TEvent domainEvent, CancellationToken ct);
}

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct);
}
```

```csharp
// Infrastructure/DomainEvents/DomainEventDispatcher.cs
using Microsoft.Extensions.DependencyInjection;
using TaskManagement.Application.Abstractions;
using TaskManagement.Domain.Common;

namespace TaskManagement.Infrastructure.DomainEvents;

internal sealed class DomainEventDispatcher(IServiceProvider provider) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct)
    {
        foreach (var domainEvent in events)
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
            foreach (dynamic handler in provider.GetServices(handlerType))
                await handler.Handle((dynamic)domainEvent, ct);
        }
    }
}
```

**Entity configuration:** see `Persistence/Configurations/TaskItemConfiguration.cs`. Every configuration has: table name, key + strongly-typed id conversion with `ValueGeneratedNever()`, `HasMaxLength` on every string, enums as strings (`HasConversion<string>().HasMaxLength(20)`), `IsRowVersion()`, `Ignore(DomainEvents)`, **`HasQueryFilter(x => !x.IsDeleted)`** for soft delete, and indexes on filter columns.

**Repository:** see `Persistence/Repositories/TaskItemRepository.cs` (`GetByIdAsync` + `Add`; the query filter already hides deleted rows). **Read-side queries:** see `Persistence/Queries/TaskQueries.cs` (`AsNoTracking`, optional filters, count + page, projection to DTO).

```csharp
// DependencyInjection.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskManagement.Application.Abstractions;
using TaskManagement.Domain.Tasks;
using TaskManagement.Infrastructure.DomainEvents;
using TaskManagement.Infrastructure.Persistence;
using TaskManagement.Infrastructure.Persistence.Repositories;

namespace TaskManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure();
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            }));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<ITaskItemRepository, TaskItemRepository>();

        return services;
    }
}
```

Packages: `Microsoft.EntityFrameworkCore.SqlServer` (8.0.31), `Microsoft.Extensions.Configuration.Abstractions` (8.0.0).

**Note on `EnableRetryOnFailure`:** with retries enabled, user-initiated transactions (`BeginTransaction`) must be wrapped in `db.Database.CreateExecutionStrategy().ExecuteAsync(...)`. A single `SaveChangesAsync` per command (the default here) needs nothing extra.

---

## 8. Api (`src/TaskManagement.Api/`)

**Contracts:** see `Contracts/Tasks/TaskRequests.cs` (`CreateTaskRequest`, `UpdateTaskRequest`, `CreatedResponse`).

**Error responses:** see `src/TaskManagement.Api/Infrastructure/`: `ApiProblems.cs` (the single builder of every error body), `ResultExtensions.cs` (`Result` → error), `ModelStateProblems.cs` (model-binding errors), and `GlobalExceptionHandler.cs` (exception → status and code mapping).

**Controller:** see `Controllers/TasksController.cs`: route `api/tasks`; handlers injected per action with `[FromServices]`; `GET` (list with `status`/`priority`/`page`/`pageSize`), `GET summary`, `GET {id:guid}`, `POST`, `PUT {id:guid}`, `DELETE {id:guid}` (soft). Literal routes such as `summary` coexist with `{id:guid}` because of the `guid` route constraint, so always constrain id parameters.

Injecting handlers per action with `[FromServices]` keeps controller constructors small as the number of endpoints grows.


**Program.cs:** see `src/TaskManagement.Api/Program.cs`. Key parts: `AddApplication()` / `AddInfrastructure()`, JSON enum converter with `allowIntegerValues: false`, `AllowInputFormatterExceptionMessages = false`, `InvalidModelStateResponseFactory = ModelStateProblems.Create`, `AddProblemDetails` with `CustomizeProblemDetails = ApiProblems.Enrich`, `AddExceptionHandler<GlobalExceptionHandler>()`, `UseExceptionHandler()` + `UseStatusCodePages()`, dev-only `MigrateAsync()`, and `public partial class Program { }`.

Packages: `Microsoft.EntityFrameworkCore.Design` (8.0.31, `PrivateAssets=all`), `Swashbuckle.AspNetCore` (6.6.2).

---

## 9. Tests

**Domain and handler tests:** see `tests/TaskManagement.Domain.UnitTests/Tasks/TaskItemTests.cs` and `tests/TaskManagement.Application.UnitTests/Tasks/` (`Fakes.cs` has an in-memory repository that mirrors the soft-delete query filter, plus a fake unit of work).

```csharp
// tests/TaskManagement.ArchitectureTests/LayerTests.cs  (package: NetArchTest.Rules)
using NetArchTest.Rules;

public class LayerTests
{
    private const string Application = "TaskManagement.Application";
    private const string Infrastructure = "TaskManagement.Infrastructure";
    private const string Api = "TaskManagement.Api";

    [Fact]
    public void Domain_has_no_outward_dependencies()
    {
        var result = Types.InAssembly(typeof(TaskManagement.Domain.Tasks.TaskItem).Assembly)
            .ShouldNot().HaveDependencyOnAny(Application, Infrastructure, Api, "Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_or_api()
    {
        var result = Types.InAssembly(typeof(TaskManagement.Application.DependencyInjection).Assembly)
            .ShouldNot().HaveDependencyOnAny(Infrastructure, Api, "Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Controllers_do_not_use_infrastructure_directly()
    {
        var result = Types.InAssembly(typeof(Program).Assembly)
            .That().ResideInNamespace("TaskManagement.Api.Controllers")
            .ShouldNot().HaveDependencyOn(Infrastructure)
            .GetResult();

        Assert.True(result.IsSuccessful);
    }
}
```
