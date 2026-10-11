using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskManagement.Api.IntegrationTests.Infrastructure;

namespace TaskManagement.Api.IntegrationTests.Tasks;

/// <summary>
/// The SeedSampleTasks migration runs in every environment (task.md T7a), so the sample rows must exist.
/// Read-only: these tests never change the seed rows.
/// </summary>
[TestFixture]
public class SeedDataTests
{
    private const string SeedFilter = "LEFT(CONVERT(char(36), Id), 32) = '5EED0000-0000-0000-0000-00000000'";

    private TestDatabase _db = null!;

    [SetUp]
    public void SetUp() => _db = new TestDatabase(TestApp.Factory.Services.GetRequiredService<IConfiguration>());

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public void Seed_inserted_30_tasks_of_which_27_are_active()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_db.Scalar($"SELECT COUNT(*) FROM Tasks WHERE {SeedFilter}"), Is.EqualTo(30));
            Assert.That(_db.Scalar($"SELECT COUNT(*) FROM Tasks WHERE {SeedFilter} AND IsDeleted = 0"), Is.EqualTo(27));
            Assert.That(_db.Scalar($"SELECT COUNT(*) FROM Tasks WHERE {SeedFilter} AND IsDeleted = 1"), Is.EqualTo(3));
        });
    }

    [Test]
    public void Seed_covers_every_status_and_priority_combination() =>
        Assert.That(_db.Scalar(
            $"SELECT COUNT(*) FROM (SELECT DISTINCT Status, Priority FROM Tasks WHERE {SeedFilter} AND IsDeleted = 0) AS c"),
            Is.EqualTo(12));

    [Test]
    public void Seed_audit_fields_are_consistent()
    {
        // CompletedAtUtc only and always for Done; ModifiedDate for every non-ToDo or deleted task;
        // DeletedDate only and always for deleted tasks; nothing modified before it was created.
        Assert.That(_db.Scalar($"""
            SELECT COUNT(*) FROM Tasks WHERE {SeedFilter} AND (
                   (Status = 'Done'  AND CompletedAtUtc IS NULL)
                OR (Status <> 'Done' AND CompletedAtUtc IS NOT NULL)
                OR ((Status <> 'ToDo' OR IsDeleted = 1) AND ModifiedDate IS NULL)
                OR (IsDeleted = 1 AND DeletedDate IS NULL)
                OR (IsDeleted = 0 AND DeletedDate IS NOT NULL)
                OR ModifiedDate < CreatedDate)
            """), Is.EqualTo(0));
    }

    [Test]
    public async Task Api_returns_active_seed_tasks_and_hides_deleted_ones()
    {
        using var client = TestApp.AuthorizedClient();

        var active = await client.GetAsync("/api/tasks/5eed0000-0000-0000-0000-000000000011");
        Assert.That(active.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var task = await active.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(task.GetProperty("title").GetString(), Is.EqualTo("Fix login timeout on mobile"));
            Assert.That(task.GetProperty("status").GetString(), Is.EqualTo("InProgress"));
            Assert.That(task.GetProperty("priority").GetString(), Is.EqualTo("Critical"));
            Assert.That(task.GetProperty("assignedTo").GetString(), Is.EqualTo("alice"));
        });

        Assert.That((await client.GetAsync("/api/tasks/5eed0000-0000-0000-0000-000000000028")).StatusCode,
            Is.EqualTo(HttpStatusCode.NotFound));
    }
}
