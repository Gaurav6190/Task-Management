using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Tasks;
using TaskManagement.Domain.Tasks;

namespace TaskManagement.Infrastructure.Persistence.Queries;

internal sealed class TaskQueries(AppDbContext db) : ITaskQueries
{
    public async Task<PagedResult<TaskDto>> ListAsync(
        TaskItemStatus? status, TaskPriority? priority, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Tasks.AsNoTracking();
        if (status is not null)
            query = query.Where(t => t.Status == status);
        if (priority is not null)
            query = query.Where(t => t.Priority == priority);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(t => t.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TaskDto(
                t.Id.Value, t.Title, t.Description, t.Status.ToString(), t.Priority.ToString(), t.AssignedTo,
                t.DueDateUtc, t.CreatedDate, t.ModifiedDate, t.CompletedAtUtc))
            .ToListAsync(ct);

        return new PagedResult<TaskDto>(items, page, pageSize, total);
    }
}
