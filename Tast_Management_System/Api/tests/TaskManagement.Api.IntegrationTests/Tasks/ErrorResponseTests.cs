using System.Net;
using System.Text;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskManagement.Api.IntegrationTests.Infrastructure;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Tasks.GetTaskSummary;
using TaskManagement.Domain.Common;
using static TaskManagement.Api.IntegrationTests.Infrastructure.ProblemAssert;

namespace TaskManagement.Api.IntegrationTests.Tasks;

/// <summary>
/// Framework-level and exception error paths (task.md T5): every one returns the standard shape.
/// Exception scenarios replace the summary handler with one that throws, so nothing touches data.
/// </summary>
[TestFixture]
public class ErrorResponseTests
{
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp() => _client = TestApp.AuthorizedClient();

    [TearDown]
    public void TearDown() => _client.Dispose();

    // ---------- Routing / content type ----------

    [Test]
    public async Task Unknown_route_is_route_not_found() =>
        await IsProblem(await _client.GetAsync("/api/does-not-exist"), HttpStatusCode.NotFound, ErrorCodes.RouteNotFound);

    [Test]
    public async Task Wrong_http_method_is_method_not_allowed() =>
        await IsProblem(await _client.DeleteAsync("/api/tasks/summary"), HttpStatusCode.MethodNotAllowed, ErrorCodes.MethodNotAllowed);

    [Test]
    public async Task Non_json_content_type_is_unsupported_media_type() =>
        await IsProblem(await _client.PostAsync("/api/tasks", new StringContent("""{ "title": "x" }""", Encoding.UTF8, "text/plain")),
            HttpStatusCode.UnsupportedMediaType, ErrorCodes.UnsupportedMediaType);

    [Test]
    public async Task Malformed_json_is_an_invalid_request_without_parser_details()
    {
        var problem = await IsProblem(await _client.PostAsync("/api/tasks", Json("""{ "title": """)),
            HttpStatusCode.BadRequest, ErrorCodes.RequestInvalid);

        Assert.Multiple(() =>
        {
            Assert.That(problem.GetRawText(), Does.Not.Contain("BytePositionInLine"));
            Assert.That(problem.GetRawText(), Does.Not.Contain("request field is required"));
        });
    }

    [Test]
    public async Task Empty_body_is_an_invalid_request() =>
        await IsProblem(await _client.PostAsync("/api/tasks", Json("")), HttpStatusCode.BadRequest, ErrorCodes.RequestInvalid);

    // ---------- Exceptions ----------

    [Test]
    public async Task Domain_exception_is_400_with_its_code_and_message()
    {
        var problem = await IsProblem(await CallSummaryThrowing(new DomainException("A deleted task cannot be modified.")),
            HttpStatusCode.BadRequest, ErrorCodes.DomainRuleViolated);

        Assert.That(problem.GetProperty("detail").GetString(), Is.EqualTo("A deleted task cannot be modified."));
    }

    [Test]
    public async Task Domain_exception_with_a_specific_code_keeps_that_code() =>
        await IsProblem(await CallSummaryThrowing(new DomainException("Locked.", "Task.Locked")), HttpStatusCode.BadRequest, "Task.Locked");

    [Test]
    public async Task Concurrency_exception_is_409_without_internal_details()
    {
        var problem = await IsProblem(await CallSummaryThrowing(new DbUpdateConcurrencyException("secret-concurrency-detail")),
            HttpStatusCode.Conflict, ErrorCodes.ConcurrencyConflict);

        Assert.That(problem.GetRawText(), Does.Not.Contain("secret-concurrency-detail"));
    }

    [Test]
    public async Task Database_update_exception_is_500_without_sql_details()
    {
        var problem = await IsProblem(await CallSummaryThrowing(new DbUpdateException("secret-sql-detail: FK_Tasks violated")),
            HttpStatusCode.InternalServerError, ErrorCodes.DatabaseError);

        Assert.That(problem.GetRawText(), Does.Not.Contain("secret-sql-detail"));
    }

    [Test]
    public async Task Sql_exception_from_raw_queries_is_500_database_error()
    {
        // A real SqlException: connect to a database name that cannot exist (fails fast, no data touched).
        SqlException sqlException;
        try
        {
            await using var connection = new SqlConnection(
                "Server=localhost;Database=TaskManagementDb_DoesNotExist;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=5");
            await connection.OpenAsync();
            throw new AssertionException("expected the connection to fail");
        }
        catch (SqlException ex)
        {
            sqlException = ex;
        }

        var problem = await IsProblem(await CallSummaryThrowing(sqlException), HttpStatusCode.InternalServerError, ErrorCodes.DatabaseError);
        Assert.That(problem.GetRawText(), Does.Not.Contain("TaskManagementDb_DoesNotExist"));
    }

    [Test]
    public async Task Unhandled_exception_is_500_without_message_type_or_stack_trace()
    {
        var problem = await IsProblem(await CallSummaryThrowing(new InvalidOperationException("secret-internal-detail")),
            HttpStatusCode.InternalServerError, ErrorCodes.ServerError);

        var raw = problem.GetRawText();
        Assert.Multiple(() =>
        {
            Assert.That(raw, Does.Not.Contain("secret-internal-detail"));
            Assert.That(raw, Does.Not.Contain("InvalidOperationException"));
            Assert.That(problem.GetProperty("detail").GetString(), Does.Contain("traceId"));
        });
    }

    [Test]
    public async Task Unhandled_exception_is_logged_with_the_same_trace_id()
    {
        var problem = await IsProblem(await CallSummaryThrowing(new InvalidOperationException("log-me")),
            HttpStatusCode.InternalServerError, ErrorCodes.ServerError);
        var traceId = problem.GetProperty("traceId").GetString()!;

        Assert.That(TestApp.Factory.Logs.Messages, Has.Some.Contains(traceId).And.Contains("log-me"));
    }

    private static async Task<HttpResponseMessage> CallSummaryThrowing(Exception exception)
    {
        await using var factory = TestApp.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            services.AddScoped<IQueryHandler<GetTaskSummaryQuery, TaskSummaryDto>>(_ => new ThrowingHandler(exception))));
        using var client = ApiFactory.WithToken(factory.CreateClient(), TestApp.AccessToken);
        return await client.GetAsync("/api/tasks/summary");
    }

    private sealed class ThrowingHandler(Exception exception) : IQueryHandler<GetTaskSummaryQuery, TaskSummaryDto>
    {
        public Task<Result<TaskSummaryDto>> Handle(GetTaskSummaryQuery query, CancellationToken ct) => throw exception;
    }
}
