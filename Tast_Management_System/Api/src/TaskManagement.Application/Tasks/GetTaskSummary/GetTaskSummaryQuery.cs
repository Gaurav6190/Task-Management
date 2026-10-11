using TaskManagement.Application.Abstractions;
using TaskManagement.Domain.Tasks;

namespace TaskManagement.Application.Tasks.GetTaskSummary;

public sealed record GetTaskSummaryQuery;

/// <summary>One row of the raw summary query: active tasks with this Status and Priority.</summary>
public sealed record TaskSummaryRow(TaskItemStatus Status, TaskPriority Priority, int Count);

/// <summary>
/// Read side of the summary. Implemented in Infrastructure with ONE raw SQL query (ADO.NET, not EF),
/// returning only combinations that have tasks. Soft-deleted tasks are excluded.
/// </summary>
public interface ITaskSummaryQuery
{
    Task<IReadOnlyList<TaskSummaryRow>> GetCountsAsync(CancellationToken ct);
}

public sealed record TaskSummaryItem(string Status, string Priority, int Count);

/// <param name="Total">Number of active (not deleted) tasks.</param>
/// <param name="ByStatusAndPriority">All 12 Status x Priority combinations, zeros included, in enum order.</param>
/// <param name="ByStatus">Totals per status (every status present).</param>
/// <param name="ByPriority">Totals per priority (every priority present).</param>
public sealed record TaskSummaryDto(
    int Total,
    IReadOnlyList<TaskSummaryItem> ByStatusAndPriority,
    IReadOnlyDictionary<string, int> ByStatus,
    IReadOnlyDictionary<string, int> ByPriority);

/// <summary>Builds the summary from the single query result; totals are added up here, not queried again.</summary>
public sealed class GetTaskSummaryQueryHandler(ITaskSummaryQuery summaryQuery)
    : IQueryHandler<GetTaskSummaryQuery, TaskSummaryDto>
{
    public async Task<Result<TaskSummaryDto>> Handle(GetTaskSummaryQuery query, CancellationToken ct)
    {
        var counts = (await summaryQuery.GetCountsAsync(ct))
            .GroupBy(r => (r.Status, r.Priority))
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Count));

        var statuses = Enum.GetValues<TaskItemStatus>();
        var priorities = Enum.GetValues<TaskPriority>();

        var cells = (
            from status in statuses
            from priority in priorities
            select new TaskSummaryItem(
                status.ToString(), priority.ToString(), counts.GetValueOrDefault((status, priority))))
            .ToList();

        var byStatus = statuses.ToDictionary(
            s => s.ToString(), s => cells.Where(c => c.Status == s.ToString()).Sum(c => c.Count));
        var byPriority = priorities.ToDictionary(
            p => p.ToString(), p => cells.Where(c => c.Priority == p.ToString()).Sum(c => c.Count));

        return new TaskSummaryDto(cells.Sum(c => c.Count), cells, byStatus, byPriority);
    }
}
