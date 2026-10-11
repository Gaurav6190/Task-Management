using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Auth;
using TaskManagement.Application.Tasks;
using TaskManagement.Application.Tasks.GetTaskSummary;
using TaskManagement.Domain.Tasks;

namespace TaskManagement.Application.UnitTests;

/// <summary>In-memory repository that mirrors the EF soft-delete query filter.</summary>
internal sealed class FakeTaskItemRepository : ITaskItemRepository
{
    public List<TaskItem> Items { get; } = [];
    public int GetCalls { get; private set; }

    public Task<TaskItem?> GetByIdAsync(TaskItemId id, CancellationToken ct = default)
    {
        GetCalls++;
        return Task.FromResult(Items.FirstOrDefault(t => t.Id == id && !t.IsDeleted));
    }

    public void Add(TaskItem task) => Items.Add(task);

    public TaskItem Seed(string title = "Write spec", DateTime? dueDateUtc = null)
    {
        var task = TaskItem.Create(title, null, TaskPriority.Medium, null, dueDateUtc, DateTime.UtcNow);
        Items.Add(task);
        return task;
    }
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        SaveCount++;
        return Task.FromResult(1);
    }
}

/// <summary>Records the arguments it was called with.</summary>
internal sealed class FakeTaskQueries : ITaskQueries
{
    public (TaskItemStatus? Status, TaskPriority? Priority, int Page, int PageSize)? LastCall { get; private set; }
    public PagedResult<TaskDto> Result { get; set; } = new([], 1, 20, 0);

    public Task<PagedResult<TaskDto>> ListAsync(
        TaskItemStatus? status, TaskPriority? priority, int page, int pageSize, CancellationToken ct)
    {
        LastCall = (status, priority, page, pageSize);
        return Task.FromResult(Result);
    }
}

internal sealed class FakeSummaryQuery(params TaskSummaryRow[] rows) : ITaskSummaryQuery
{
    public Task<IReadOnlyList<TaskSummaryRow>> GetCountsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TaskSummaryRow>>(rows);
}

/// <summary>Accepts exactly one username (case-insensitive) / password pair.</summary>
internal sealed class FakeAuthenticator(string username, string password) : IUserAuthenticator
{
    public List<(string Username, string Password)> Calls { get; } = [];

    public Task<AuthenticatedUser?> AuthenticateAsync(string user, string pass, CancellationToken ct)
    {
        Calls.Add((user, pass));
        var ok = string.Equals(user, username, StringComparison.OrdinalIgnoreCase) && pass == password;
        return Task.FromResult(ok ? new AuthenticatedUser(username) : null);
    }
}

internal sealed class FakeTokenService : IAccessTokenService
{
    public static readonly DateTime Expires = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public List<AuthenticatedUser> Issued { get; } = [];

    public AccessToken Issue(AuthenticatedUser user)
    {
        Issued.Add(user);
        return new AccessToken($"token-for-{user.Username}", Expires);
    }
}
