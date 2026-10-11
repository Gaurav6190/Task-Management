using TaskManagement.Application.Abstractions;
using TaskManagement.Domain.Tasks;

namespace TaskManagement.Application.Tasks;

/// <summary>Read-side queries; implemented in Infrastructure with no-tracking projections.</summary>
public interface ITaskQueries
{
    Task<PagedResult<TaskDto>> ListAsync(
        TaskItemStatus? status, TaskPriority? priority, int page, int pageSize, CancellationToken ct);
}
