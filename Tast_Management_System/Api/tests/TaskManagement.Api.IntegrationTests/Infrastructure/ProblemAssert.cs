using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace TaskManagement.Api.IntegrationTests.Infrastructure;

/// <summary>Asserts the standard error shape (task.md T5) shared by every error response.</summary>
public static class ProblemAssert
{
    public static async Task<JsonElement> IsProblem(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(status), body);
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));

        var problem = JsonDocument.Parse(body).RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(problem.GetProperty("status").GetInt32(), Is.EqualTo((int)status));
            Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo(code));
            Assert.That(problem.GetProperty("title").GetString(), Is.Not.Null.And.Not.Empty);
            Assert.That(problem.GetProperty("type").GetString(), Is.Not.Null.And.Not.Empty);
            Assert.That(problem.GetProperty("instance").GetString(), Does.StartWith("/"));
            Assert.That(problem.GetProperty("traceId").GetString(), Is.Not.Null.And.Not.Empty);
            Assert.That(body, Does.Not.Contain("   at "), "no stack traces");
        });
        return problem;
    }

    public static string[] FieldErrors(JsonElement problem, string field) =>
        problem.GetProperty("errors").GetProperty(field).EnumerateArray().Select(e => e.GetString()!).ToArray();

    public static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");
}
