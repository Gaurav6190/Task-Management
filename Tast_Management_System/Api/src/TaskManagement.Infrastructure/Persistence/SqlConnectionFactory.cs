using Microsoft.Data.SqlClient;

namespace TaskManagement.Infrastructure.Persistence;

/// <summary>
/// Opens plain ADO.NET connections for raw SQL queries that deliberately bypass EF Core.
/// Uses the same "Default" connection string as AppDbContext (Windows authentication).
/// </summary>
internal sealed class SqlConnectionFactory(string connectionString)
{
    public async Task<SqlConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}
