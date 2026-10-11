-- Task summary: count of active tasks grouped by Status and Priority (task.md T7b).
-- Raw SQL executed with ADO.NET (Microsoft.Data.SqlClient), not EF Core.
-- Raw SQL bypasses EF's soft-delete query filter, so deleted tasks are excluded explicitly.
SELECT   t.Status,
         t.Priority,
         COUNT(*) AS TaskCount
FROM     dbo.Tasks AS t
WHERE    t.IsDeleted = 0
GROUP BY t.Status, t.Priority
ORDER BY t.Status, t.Priority;
