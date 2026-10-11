using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskManagement.Api.IntegrationTests.Infrastructure;

namespace TaskManagement.Api.IntegrationTests.Tasks;

/// <summary>GET /api/tasks/summary against the real database (task.md T7b).</summary>
[TestFixture]
public class TaskSummaryTests
{
    private static readonly string[] Statuses = ["ToDo", "InProgress", "Done"];
    private static readonly string[] Priorities = ["Low", "Medium", "High", "Critical"];

    private HttpClient _client = null!;
    private TestDatabase _db = null!;

    [SetUp]
    public void SetUp()
    {
        _client = TestApp.AuthorizedClient();
        _db = new TestDatabase(TestApp.Factory.Services.GetRequiredService<IConfiguration>());
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        _client.Dispose();
    }

    [Test]
    public async Task Summary_matches_a_direct_group_by_query_on_the_database()
    {
        var summary = await GetSummary();
        var sql = _db.GroupedCounts();

        Assert.Multiple(() =>
        {
            foreach (var cell in summary.GetProperty("byStatusAndPriority").EnumerateArray())
            {
                var key = (cell.GetProperty("status").GetString()!, cell.GetProperty("priority").GetString()!);
                Assert.That(cell.GetProperty("count").GetInt32(), Is.EqualTo(sql.GetValueOrDefault(key)), key.ToString());
            }
            Assert.That(summary.GetProperty("total").GetInt32(), Is.EqualTo(sql.Values.Sum()));
        });
    }

    [Test]
    public async Task Summary_has_all_12_combinations_in_order_and_consistent_totals()
    {
        var summary = await GetSummary();
        var cells = summary.GetProperty("byStatusAndPriority").EnumerateArray()
            .Select(c => (Status: c.GetProperty("status").GetString()!, Priority: c.GetProperty("priority").GetString()!,
                Count: c.GetProperty("count").GetInt32()))
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(cells.Select(c => (c.Status, c.Priority)),
                Is.EqualTo(from s in Statuses from p in Priorities select (s, p)));
            Assert.That(summary.GetProperty("total").GetInt32(), Is.EqualTo(cells.Sum(c => c.Count)));
            foreach (var s in Statuses)
                Assert.That(summary.GetProperty("byStatus").GetProperty(s).GetInt32(), Is.EqualTo(cells.Where(c => c.Status == s).Sum(c => c.Count)));
            foreach (var p in Priorities)
                Assert.That(summary.GetProperty("byPriority").GetProperty(p).GetInt32(), Is.EqualTo(cells.Where(c => c.Priority == p).Sum(c => c.Count)));
        });
    }

    [Test]
    public async Task Inserted_tasks_change_the_counts_by_exactly_that_amount_and_deleted_ones_are_ignored()
    {
        var before = await Counts();

        _db.InsertTask("a", "ToDo", "High");
        _db.InsertTask("b", "ToDo", "High");
        _db.InsertTask("c", "Done", "Low");
        _db.InsertTask("deleted", "InProgress", "Critical", isDeleted: true);

        var after = await Counts();
        Assert.Multiple(() =>
        {
            Assert.That(after[("ToDo", "High")], Is.EqualTo(before[("ToDo", "High")] + 2));
            Assert.That(after[("Done", "Low")], Is.EqualTo(before[("Done", "Low")] + 1));
            Assert.That(after[("InProgress", "Critical")], Is.EqualTo(before[("InProgress", "Critical")]));
            Assert.That(after.Values.Sum(), Is.EqualTo(before.Values.Sum() + 3));
        });
    }

    [Test]
    public async Task Soft_deleting_through_the_api_lowers_the_count()
    {
        var id = _db.InsertTask("to delete", "InProgress", "Medium");
        var before = await Counts();

        Assert.That((await _client.DeleteAsync($"/api/tasks/{id}")).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        Assert.That((await Counts())[("InProgress", "Medium")], Is.EqualTo(before[("InProgress", "Medium")] - 1));
    }

    [Test]
    public async Task Rows_with_unknown_status_or_priority_are_skipped_and_the_summary_still_works()
    {
        var before = await GetSummary();
        _db.InsertTask("bad status", "Archived", "Low");
        _db.InsertTask("bad priority", "ToDo", "Urgent");

        var after = await GetSummary();

        Assert.That(after.GetProperty("total").GetInt32(), Is.EqualTo(before.GetProperty("total").GetInt32()));
        Assert.That(TestApp.Factory.Logs.Messages, Has.Some.Contains("unknown Status 'Archived'"));
    }

    private async Task<JsonElement> GetSummary()
    {
        var response = await _client.GetAsync("/api/tasks/summary");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<Dictionary<(string, string), int>> Counts() =>
        (await GetSummary()).GetProperty("byStatusAndPriority").EnumerateArray().ToDictionary(
            c => (c.GetProperty("status").GetString()!, c.GetProperty("priority").GetString()!),
            c => c.GetProperty("count").GetInt32());
}
