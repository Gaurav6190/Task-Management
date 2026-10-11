using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using TaskManagement.Application.Tasks.GetTaskSummary;
using TaskManagement.Domain.Tasks;

namespace TaskManagement.Infrastructure.Persistence.Queries;

/// <summary>
/// Task summary via ONE raw SQL query (Persistence/Queries/Sql/TaskSummary.sql) executed with
/// ADO.NET. No EF Core is involved in this path (task.md T7b).
/// </summary>
internal sealed class TaskSummaryQuery(SqlConnectionFactory connections, ILogger<TaskSummaryQuery> logger)
    : ITaskSummaryQuery
{
    private static readonly string Sql = SqlScripts.Read("TaskSummary.sql");
    private const int CommandTimeoutSeconds = 30;

    public async Task<IReadOnlyList<TaskSummaryRow>> GetCountsAsync(CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        await using var command = new SqlCommand(Sql, connection)
        {
            CommandType = CommandType.Text,
            CommandTimeout = CommandTimeoutSeconds
        };

        var rows = new List<TaskSummaryRow>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var status = reader.GetString(0);
            var priority = reader.GetString(1);
            var count = reader.GetInt32(2);

            if (Enum.TryParse<TaskItemStatus>(status, out var s) && Enum.IsDefined(s) &&
                Enum.TryParse<TaskPriority>(priority, out var p) && Enum.IsDefined(p))
            {
                rows.Add(new TaskSummaryRow(s, p, count));
            }
            else
            {
                // Bad data must not break the endpoint; surface it in the logs instead.
                logger.LogWarning(
                    "Task summary skipped {Count} row(s) with unknown Status '{Status}' / Priority '{Priority}'",
                    count, status, priority);
            }
        }

        return rows;
    }
}
