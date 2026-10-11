using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskManagement.Api.IntegrationTests.Infrastructure;
using TaskManagement.Application.Abstractions;
using static TaskManagement.Api.IntegrationTests.Infrastructure.ProblemAssert;

namespace TaskManagement.Api.IntegrationTests.Tasks;

/// <summary>
/// List / Get / Create / Update / Delete against the real database: positive and negative scenarios.
/// API responses are compared with rows read and written through raw SQL. Every row a test creates
/// carries the test's marker and is removed in TearDown.
/// </summary>
[TestFixture]
public class TaskApiTests
{
    private const string Tasks = "/api/tasks";

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

    // =====================================================================================
    // GET /api/tasks/{id}
    // =====================================================================================

    [Test]
    public async Task Get_returns_the_row_exactly_as_stored()
    {
        var created = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddTicks(1234567);
        var modified = created.AddHours(5);
        var due = new DateTime(2030, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var id = _db.InsertTask("Stored row", "InProgress", "Critical", description: "From SQL",
            assignedTo: "it-user", dueDateUtc: due, createdDate: created, modifiedDate: modified);

        var task = await GetTask(id);

        Assert.Multiple(() =>
        {
            Assert.That(task.GetProperty("id").GetGuid(), Is.EqualTo(id));
            Assert.That(task.GetProperty("title").GetString(), Is.EqualTo(_db.Title("Stored row")));
            Assert.That(task.GetProperty("description").GetString(), Is.EqualTo("From SQL"));
            Assert.That(task.GetProperty("status").GetString(), Is.EqualTo("InProgress"));
            Assert.That(task.GetProperty("priority").GetString(), Is.EqualTo("Critical"));
            Assert.That(task.GetProperty("assignedTo").GetString(), Is.EqualTo("it-user"));
            AssertUtc(due, task.GetProperty("dueDateUtc"));
            AssertUtc(created, task.GetProperty("createdDate"));
            AssertUtc(modified, task.GetProperty("modifiedDate"));
            Assert.That(task.GetProperty("completedAtUtc").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(task.TryGetProperty("isDeleted", out _), Is.False, "IsDeleted must not be exposed");
        });
    }

    [Test]
    public async Task Get_unknown_id_is_task_not_found()
    {
        var id = Guid.NewGuid();

        var problem = await IsProblem(await _client.GetAsync($"{Tasks}/{id}"), HttpStatusCode.NotFound, ErrorCodes.TaskNotFound);

        Assert.That(problem.GetProperty("detail").GetString(), Does.Contain(id.ToString()));
    }

    [Test]
    public async Task Get_soft_deleted_id_is_task_not_found()
    {
        var id = _db.InsertTask("Deleted", "ToDo", "Low", isDeleted: true);

        await IsProblem(await _client.GetAsync($"{Tasks}/{id}"), HttpStatusCode.NotFound, ErrorCodes.TaskNotFound);
    }

    [Test]
    public async Task Get_with_a_non_guid_id_is_route_not_found() =>
        await IsProblem(await _client.GetAsync($"{Tasks}/abc"), HttpStatusCode.NotFound, ErrorCodes.RouteNotFound);

    // =====================================================================================
    // GET /api/tasks (list)
    // =====================================================================================

    [Test]
    public async Task List_filters_return_exactly_the_matching_database_rows()
    {
        var match = _db.InsertTask("ToDo High", "ToDo", "High");
        _db.InsertTask("Done High", "Done", "High");
        _db.InsertTask("ToDo Low", "ToDo", "Low");
        _db.InsertTask("Deleted ToDo High", "ToDo", "High", isDeleted: true);

        var byBoth = await ListMine("?status=ToDo&priority=High&pageSize=100");
        var byStatus = await ListMine("?status=ToDo&pageSize=100");
        var byPriority = await ListMine("?priority=High&pageSize=100");
        var all = await ListMine("?pageSize=100");

        Assert.Multiple(() =>
        {
            Assert.That(byBoth, Is.EqualTo(new[] { match }));
            Assert.That(byStatus, Has.Count.EqualTo(_db.CountActive(status: "ToDo")));
            Assert.That(byPriority, Has.Count.EqualTo(_db.CountActive(priority: "High")));
            Assert.That(all, Has.Count.EqualTo(3), "the soft-deleted row is never returned");
        });
    }

    [TestCase("done", "high")]
    [TestCase("DONE", "HIGH")]
    public async Task List_filters_are_case_insensitive(string status, string priority)
    {
        var id = _db.InsertTask("Done High", "Done", "High");

        Assert.That(await ListMine($"?status={status}&priority={priority}&pageSize=100"), Is.EqualTo(new[] { id }));
    }

    [Test]
    public async Task List_paging_returns_consecutive_non_overlapping_pages_newest_first()
    {
        for (var i = 0; i < 6; i++)
            _db.InsertTask($"Page item {i}", "ToDo", "Low", createdDate: DateTime.UtcNow.AddSeconds(i));

        var page1 = await ListPage("?page=1&pageSize=5");
        var page2 = await ListPage("?page=2&pageSize=5");
        var total = Convert.ToInt32(_db.Scalar("SELECT COUNT(*) FROM Tasks WHERE IsDeleted = 0"));

        var ids1 = Ids(page1);
        var ids2 = Ids(page2);
        Assert.Multiple(() =>
        {
            Assert.That(page1.GetProperty("page").GetInt32(), Is.EqualTo(1));
            Assert.That(page2.GetProperty("page").GetInt32(), Is.EqualTo(2));
            Assert.That(page1.GetProperty("pageSize").GetInt32(), Is.EqualTo(5));
            Assert.That(page1.GetProperty("totalCount").GetInt32(), Is.EqualTo(total));
            Assert.That(page1.GetProperty("totalPages").GetInt32(), Is.EqualTo((int)Math.Ceiling(total / 5.0)));
            Assert.That(ids1, Has.Count.EqualTo(5));
            Assert.That(ids2, Has.Count.EqualTo(Math.Min(5, total - 5)));
            Assert.That(ids1.Intersect(ids2), Is.Empty);
            Assert.That(Titles(page1).Take(5), Is.All.StartsWith(_db.Marker), "the newest rows (ours) come first");
        });
    }

    [Test]
    public async Task List_page_beyond_the_end_is_empty_but_reports_totals()
    {
        var page = await ListPage("?page=100000&pageSize=100");

        Assert.Multiple(() =>
        {
            Assert.That(page.GetProperty("items").GetArrayLength(), Is.Zero);
            Assert.That(page.GetProperty("totalCount").GetInt32(), Is.GreaterThan(0));
        });
    }

    [TestCase("?status=Bogus", "Status")]
    [TestCase("?priority=Urgent", "Priority")]
    [TestCase("?page=0", "Page")]
    [TestCase("?pageSize=0", "PageSize")]
    [TestCase("?pageSize=101", "PageSize")]
    public async Task List_with_invalid_query_reports_the_field(string query, string field)
    {
        var problem = await IsProblem(await _client.GetAsync(Tasks + query), HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);

        Assert.That(FieldErrors(problem, field), Is.Not.Empty);
    }

    [Test]
    public async Task List_with_non_numeric_page_is_a_validation_error() =>
        Assert.That(FieldErrors(await IsProblem(await _client.GetAsync($"{Tasks}?page=abc"),
            HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed), "Page"), Is.Not.Empty);

    // =====================================================================================
    // POST /api/tasks
    // =====================================================================================

    [Test]
    public async Task Post_stores_exactly_the_values_sent()
    {
        var before = DateTime.UtcNow.AddSeconds(-5);
        var response = await _client.PostAsync(Tasks, Json($$"""
            { "title": "{{_db.Title("Posted")}}", "description": "Via API", "priority": "high",
              "assignedTo": "  alice  ", "dueDateUtc": "2031-01-15T00:00:00Z" }
            """));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var row = _db.GetRow(id)!;
        Assert.Multiple(() =>
        {
            Assert.That(response.Headers.Location!.ToString(), Does.EndWith($"/api/tasks/{id}"));
            Assert.That(row["Title"], Is.EqualTo(_db.Title("Posted")));
            Assert.That(row["Description"], Is.EqualTo("Via API"));
            Assert.That(row["Status"], Is.EqualTo("ToDo"));
            Assert.That(row["Priority"], Is.EqualTo("High"), "stored by canonical name");
            Assert.That(row["AssignedTo"], Is.EqualTo("alice"), "trimmed by the domain");
            Assert.That(row["DueDateUtc"], Is.EqualTo(new DateTime(2031, 1, 15)));
            Assert.That((DateTime)row["CreatedDate"]!, Is.InRange(before, DateTime.UtcNow.AddSeconds(5)));
            Assert.That(row["ModifiedDate"], Is.Null);
            Assert.That(row["IsDeleted"], Is.EqualTo(false));
        });
    }

    [Test]
    public async Task Post_with_only_title_and_priority_uses_defaults()
    {
        var response = await _client.PostAsync(Tasks, Json($$"""{ "title": "{{_db.Title("Minimal")}}", "priority": "Low" }"""));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var task = await GetTask((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        Assert.Multiple(() =>
        {
            Assert.That(task.GetProperty("status").GetString(), Is.EqualTo("ToDo"));
            Assert.That(task.GetProperty("description").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(task.GetProperty("assignedTo").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(task.GetProperty("dueDateUtc").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(task.GetProperty("modifiedDate").ValueKind, Is.EqualTo(JsonValueKind.Null));
        });
    }

    [Test]
    public async Task Post_accepts_values_exactly_at_the_maximum_lengths()
    {
        var title = _db.Title("").PadRight(200, 't');
        var response = await _client.PostAsJsonAsync(Tasks, new
        {
            title, description = new string('d', 4000), priority = "Medium", assignedTo = new string('a', 100)
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());
        var row = _db.GetRow((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid())!;
        Assert.Multiple(() =>
        {
            Assert.That(((string)row["Title"]!).Length, Is.EqualTo(200));
            Assert.That(((string)row["Description"]!).Length, Is.EqualTo(4000));
            Assert.That(((string)row["AssignedTo"]!).Length, Is.EqualTo(100));
        });
    }

    [TestCase("title", 201, "Title")]
    [TestCase("description", 4001, "Description")]
    [TestCase("assignedTo", 101, "AssignedTo")]
    public async Task Post_with_a_too_long_field_is_rejected(string jsonField, int length, string errorField)
    {
        var body = new Dictionary<string, object?> { ["title"] = _db.Title("Too long"), ["priority"] = "Low" };
        body[jsonField] = new string('x', length);

        var problem = await IsProblem(await _client.PostAsJsonAsync(Tasks, body), HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);

        Assert.That(FieldErrors(problem, errorField), Is.Not.Empty);
        Assert.That(_db.CountActive(), Is.Zero, "nothing was stored");
    }

    [TestCase("""{ "title": "", "priority": "Low" }""", "Title", "Title is required.")]
    [TestCase("""{ "priority": "Low" }""", "Title", "Title is required.")]
    [TestCase("""{ "title": "   ", "priority": "Low" }""", "Title", "Title is required.")]
    [TestCase("""{ "title": "x" }""", "Priority", "Priority is required.")]
    [TestCase("""{ "title": "x", "priority": "Urgent" }""", "Priority", "Priority must be one of: Low, Medium, High, Critical.")]
    public async Task Post_with_missing_or_invalid_field_is_a_field_validation_error(string json, string field, string message)
    {
        var problem = await IsProblem(await _client.PostAsync(Tasks, Json(json)), HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);

        Assert.That(FieldErrors(problem, field), Is.EqualTo(new[] { message }));
    }

    [Test]
    public async Task Post_with_a_due_date_in_the_past_is_rejected()
    {
        var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-ddT00:00:00Z");
        var response = await _client.PostAsync(Tasks, Json($$"""{ "title": "x", "priority": "Low", "dueDateUtc": "{{yesterday}}" }"""));

        var problem = await IsProblem(response, HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);
        Assert.That(FieldErrors(problem, "DueDateUtc"), Is.EqualTo(new[] { "DueDateUtc cannot be in the past." }));
    }

    [Test]
    public async Task Post_with_a_numeric_priority_is_an_invalid_request_without_internals()
    {
        var problem = await IsProblem(await _client.PostAsync(Tasks, Json("""{ "title": "x", "priority": 2 }""")),
            HttpStatusCode.BadRequest, ErrorCodes.RequestInvalid);

        Assert.Multiple(() =>
        {
            Assert.That(FieldErrors(problem, "Priority"), Is.Not.Empty);
            Assert.That(problem.GetRawText(), Does.Not.Contain("TaskManagement."));
        });
    }

    // =====================================================================================
    // PUT /api/tasks/{id}
    // =====================================================================================

    [Test]
    public async Task Put_updates_the_database_row()
    {
        var id = _db.InsertTask("Before update", "ToDo", "Low");

        var response = await _client.PutAsync($"{Tasks}/{id}", Json($$"""
            { "title": "{{_db.Title("After update")}}", "description": "Changed", "status": "Done",
              "priority": "Medium", "assignedTo": "bob" }
            """));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var row = _db.GetRow(id)!;
        Assert.Multiple(() =>
        {
            Assert.That(row["Title"], Is.EqualTo(_db.Title("After update")));
            Assert.That(row["Description"], Is.EqualTo("Changed"));
            Assert.That(row["Status"], Is.EqualTo("Done"));
            Assert.That(row["Priority"], Is.EqualTo("Medium"));
            Assert.That(row["AssignedTo"], Is.EqualTo("bob"));
            Assert.That(row["ModifiedDate"], Is.Not.Null);
            Assert.That(row["CompletedAtUtc"], Is.Not.Null);
        });
    }

    [Test]
    public async Task Put_moving_a_done_task_back_to_todo_clears_the_completed_date()
    {
        var id = _db.InsertTask("Round trip", "ToDo", "Low");
        await Put(id, "Done");
        Assert.That(_db.GetRow(id)!["CompletedAtUtc"], Is.Not.Null);

        await Put(id, "ToDo");

        Assert.That(_db.GetRow(id)!["CompletedAtUtc"], Is.Null);
    }

    [Test]
    public async Task Put_unknown_id_is_task_not_found() =>
        await IsProblem(await PutRaw(Guid.NewGuid(), "ToDo"), HttpStatusCode.NotFound, ErrorCodes.TaskNotFound);

    [Test]
    public async Task Put_soft_deleted_task_is_task_not_found_and_leaves_the_row_unchanged()
    {
        var id = _db.InsertTask("Deleted", "ToDo", "Low", isDeleted: true);

        await IsProblem(await PutRaw(id, "Done"), HttpStatusCode.NotFound, ErrorCodes.TaskNotFound);

        Assert.That(_db.GetRow(id)!["Status"], Is.EqualTo("ToDo"));
    }

    [TestCase("Open", "Status")]
    [TestCase("", "Status")]
    public async Task Put_with_invalid_status_is_a_field_validation_error(string status, string field)
    {
        var problem = await IsProblem(await PutRaw(Guid.NewGuid(), status), HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);

        Assert.That(FieldErrors(problem, field), Is.Not.Empty);
    }

    [Test]
    public async Task Put_moving_the_due_date_into_the_past_violates_a_domain_rule()
    {
        var id = _db.InsertTask("Due date", "ToDo", "Low", dueDateUtc: DateTime.UtcNow.Date.AddDays(10));
        var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-ddT00:00:00Z");

        var response = await _client.PutAsync($"{Tasks}/{id}", Json($$"""
            { "title": "{{_db.Title("Due date")}}", "status": "ToDo", "priority": "Low", "dueDateUtc": "{{yesterday}}" }
            """));

        var problem = await IsProblem(response, HttpStatusCode.BadRequest, ErrorCodes.DomainRuleViolated);
        Assert.That(problem.GetProperty("detail").GetString(), Is.EqualTo("Due date cannot be in the past."));
    }

    // =====================================================================================
    // DELETE /api/tasks/{id}
    // =====================================================================================

    [Test]
    public async Task Delete_keeps_the_row_but_hides_it_from_the_api()
    {
        var id = _db.InsertTask("To delete", "ToDo", "Low");

        Assert.That((await _client.DeleteAsync($"{Tasks}/{id}")).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var row = _db.GetRow(id);
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(row, Is.Not.Null, "not removed from the database");
            Assert.That(row!["IsDeleted"], Is.EqualTo(true));
            Assert.That(row["DeletedDate"], Is.Not.Null);
            Assert.That(row["ModifiedDate"], Is.Not.Null);
            Assert.That(await ListMine("?pageSize=100"), Does.Not.Contain(id));
        });
        await IsProblem(await _client.GetAsync($"{Tasks}/{id}"), HttpStatusCode.NotFound, ErrorCodes.TaskNotFound);
    }

    [Test]
    public async Task Delete_twice_is_task_not_found()
    {
        var id = _db.InsertTask("Delete twice", "ToDo", "Low");
        await _client.DeleteAsync($"{Tasks}/{id}");

        await IsProblem(await _client.DeleteAsync($"{Tasks}/{id}"), HttpStatusCode.NotFound, ErrorCodes.TaskNotFound);
    }

    [Test]
    public async Task Delete_unknown_id_is_task_not_found() =>
        await IsProblem(await _client.DeleteAsync($"{Tasks}/{Guid.NewGuid()}"), HttpStatusCode.NotFound, ErrorCodes.TaskNotFound);

    // =====================================================================================
    // Helpers
    // =====================================================================================

    private async Task<JsonElement> GetTask(Guid id)
    {
        var response = await _client.GetAsync($"{Tasks}/{id}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement> ListPage(string query)
    {
        var response = await _client.GetAsync(Tasks + query);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Ids from a list call, limited to this test's own rows (seed and other data may exist).</summary>
    private async Task<List<Guid>> ListMine(string query) =>
        (await ListPage(query)).GetProperty("items").EnumerateArray()
            .Where(t => t.GetProperty("title").GetString()!.StartsWith(_db.Marker, StringComparison.Ordinal))
            .Select(t => t.GetProperty("id").GetGuid())
            .ToList();

    private static List<Guid> Ids(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("id").GetGuid()).ToList();

    private static List<string> Titles(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("title").GetString()!).ToList();

    private Task<HttpResponseMessage> PutRaw(Guid id, string status) =>
        _client.PutAsync($"{Tasks}/{id}", Json($$"""{ "title": "{{_db.Title("Put")}}", "status": "{{status}}", "priority": "Low" }"""));

    private async Task Put(Guid id, string status) =>
        Assert.That((await PutRaw(id, status)).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

    /// <summary>API dates must be UTC ("Z") and equal to the stored datetime2 value to the tick.</summary>
    private static void AssertUtc(DateTime expected, JsonElement actual)
    {
        Assert.That(actual.GetString(), Does.EndWith("Z"));
        var parsed = actual.GetDateTime();
        Assert.That(parsed.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(parsed.Ticks, Is.EqualTo(expected.Ticks));
    }
}
