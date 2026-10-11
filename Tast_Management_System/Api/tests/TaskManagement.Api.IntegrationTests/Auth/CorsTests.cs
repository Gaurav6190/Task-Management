using System.Net;

namespace TaskManagement.Api.IntegrationTests.Auth;

/// <summary>CORS: only the React UI origin (https://localhost:5173, task.md D20) may call the API from a browser.</summary>
[TestFixture]
public class CorsTests
{
    private static HttpRequestMessage Preflight(string origin, string method = "GET", string headers = "authorization,content-type")
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/tasks");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", headers);
        return request;
    }

    [TestCase("GET")]
    [TestCase("POST")]
    [TestCase("PUT")]
    [TestCase("DELETE")]
    public async Task Preflight_from_the_ui_origin_is_allowed(string method)
    {
        using var client = TestApp.AnonymousClient();

        var response = await client.SendAsync(Preflight("https://localhost:5173", method));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(response.Headers.GetValues("Access-Control-Allow-Origin"), Is.EqualTo(new[] { "https://localhost:5173" }));
            Assert.That(string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers")).ToLowerInvariant(),
                Does.Contain("authorization").And.Contain("content-type"));
        });
    }

    [TestCase("https://evil.example")]
    [TestCase("http://localhost:5173")] // http, not https
    [TestCase("https://localhost:3000")]
    public async Task Preflight_from_other_origins_gets_no_cors_headers(string origin)
    {
        using var client = TestApp.AnonymousClient();

        var response = await client.SendAsync(Preflight(origin));

        Assert.That(response.Headers.Contains("Access-Control-Allow-Origin"), Is.False);
    }

    [Test]
    public async Task Preflight_for_a_disallowed_method_does_not_allow_it()
    {
        using var client = TestApp.AnonymousClient();

        var response = await client.SendAsync(Preflight("https://localhost:5173", "PATCH"));

        // ASP.NET lists the allowed methods; the browser then blocks PATCH because it isn't listed.
        var allowed = response.Headers.TryGetValues("Access-Control-Allow-Methods", out var values)
            ? string.Join(",", values) : string.Empty;
        Assert.Multiple(() =>
        {
            Assert.That(allowed, Does.Not.Contain("PATCH"));
            Assert.That(allowed, Does.Contain("GET").And.Contain("POST").And.Contain("PUT").And.Contain("DELETE"));
        });
    }

    [Test]
    public async Task Actual_request_from_the_ui_origin_gets_the_allow_origin_header()
    {
        using var client = TestApp.AuthorizedClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/tasks");
        request.Headers.Add("Origin", "https://localhost:5173");

        var response = await client.SendAsync(request);

        Assert.That(response.Headers.GetValues("Access-Control-Allow-Origin"), Is.EqualTo(new[] { "https://localhost:5173" }));
    }
}
