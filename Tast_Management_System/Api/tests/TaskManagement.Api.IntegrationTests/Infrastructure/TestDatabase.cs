using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace TaskManagement.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Direct SQL access to the real database the API uses (same connection string, Windows auth),
/// so tests can check API responses against what is actually stored, independently of EF Core.
/// Every row a test creates carries <see cref="Marker"/> in its title and is removed in Dispose.
/// (Matched with LEFT(...) = @Marker, not LIKE: '[' is a LIKE wildcard.)
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly string _connectionString;

    public TestDatabase(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");
        Marker = $"[IT-{Guid.NewGuid().ToString("N")[..8]}]";
    }

    /// <summary>Unique per test instance, e.g. "[IT-3f2a9c1d]".</summary>
    public string Marker { get; }

    public string Title(string name) => $"{Marker} {name}";

    public Guid InsertTask(
        string name, string status, string priority, string? description = null, string? assignedTo = null,
        DateTime? dueDateUtc = null, DateTime? createdDate = null, DateTime? modifiedDate = null, bool isDeleted = false)
    {
        var id = Guid.NewGuid();
        Execute("""
            INSERT INTO Tasks (Id, Title, Description, Status, Priority, AssignedTo, DueDateUtc,
                               CreatedDate, ModifiedDate, IsDeleted, DeletedDate)
            VALUES (@Id, @Title, @Description, @Status, @Priority, @AssignedTo, @DueDateUtc,
                    @CreatedDate, @ModifiedDate, @IsDeleted, @DeletedDate)
            """,
            ("@Id", id), ("@Title", Title(name)), ("@Description", description), ("@Status", status),
            ("@Priority", priority), ("@AssignedTo", assignedTo), ("@DueDateUtc", dueDateUtc),
            ("@CreatedDate", createdDate ?? DateTime.UtcNow), ("@ModifiedDate", modifiedDate),
            ("@IsDeleted", isDeleted), ("@DeletedDate", isDeleted ? DateTime.UtcNow : null));
        return id;
    }

    /// <summary>Reads a row ignoring the soft-delete filter (raw SQL, not EF).</summary>
    public Dictionary<string, object?>? GetRow(Guid id)
    {
        using var connection = Open();
        using var command = new SqlCommand("SELECT * FROM Tasks WHERE Id = @Id", connection);
        command.Parameters.AddWithValue("@Id", id);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;

        return Enumerable.Range(0, reader.FieldCount)
            .ToDictionary(reader.GetName, i => reader.IsDBNull(i) ? null : reader.GetValue(i));
    }

    public int CountActive(string? status = null, string? priority = null)
    {
        using var connection = Open();
        using var command = new SqlCommand("""
            SELECT COUNT(*) FROM Tasks
            WHERE LEFT(Title, LEN(@Marker)) = @Marker AND IsDeleted = 0
              AND (@Status IS NULL OR Status = @Status)
              AND (@Priority IS NULL OR Priority = @Priority)
            """, connection);
        command.Parameters.AddWithValue("@Marker", Marker);
        command.Parameters.AddWithValue("@Status", (object?)status ?? DBNull.Value);
        command.Parameters.AddWithValue("@Priority", (object?)priority ?? DBNull.Value);
        return (int)command.ExecuteScalar();
    }

    /// <summary>
    /// Active-task counts per (Status, Priority), straight from SQL Server. Written here
    /// independently of the API's TaskSummary.sql so the test is a genuine cross-check.
    /// </summary>
    public Dictionary<(string Status, string Priority), int> GroupedCounts()
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "SELECT Status, Priority, COUNT(*) FROM Tasks WHERE IsDeleted = 0 GROUP BY Status, Priority", connection);
        using var reader = command.ExecuteReader();
        var counts = new Dictionary<(string, string), int>();
        while (reader.Read())
            counts[(reader.GetString(0), reader.GetString(1))] = reader.GetInt32(2);
        return counts;
    }

    public object? Scalar(string sql)
    {
        using var connection = Open();
        using var command = new SqlCommand(sql, connection);
        return command.ExecuteScalar();
    }

    /// <summary>Removes every row this test created (hard delete is fine for test data).</summary>
    public void Dispose() =>
        Execute("DELETE FROM Tasks WHERE LEFT(Title, LEN(@Marker)) = @Marker", ("@Marker", Marker));

    private void Execute(string sql, params (string Name, object? Value)[] parameters)
    {
        using var connection = Open();
        using var command = new SqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            var parameter = command.Parameters.AddWithValue(name, value ?? DBNull.Value);
            // AddWithValue infers legacy 'datetime' (~3 ms precision) for DateTime; the column is datetime2.
            if (value is DateTime)
                parameter.SqlDbType = System.Data.SqlDbType.DateTime2;
        }
        command.ExecuteNonQuery();
    }

    private SqlConnection Open()
    {
        var connection = new SqlConnection(_connectionString);
        connection.Open();
        return connection;
    }
}
