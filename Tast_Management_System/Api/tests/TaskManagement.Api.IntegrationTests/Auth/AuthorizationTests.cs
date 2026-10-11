using System.Net;
using System.Net.Http.Headers;
using TaskManagement.Api.IntegrationTests.Infrastructure;
using TaskManagement.Application.Abstractions;

namespace TaskManagement.Api.IntegrationTests.Auth;

/// <summary>
/// Every /api/tasks endpoint requires a valid access token (task.md T8b / D15). Each endpoint is
/// tested with every kind of missing or invalid token, plus a valid one. These tests never write:
/// bodies and ids are chosen so an authorized call ends in 400/404 instead of creating data.
/// </summary>
[TestFixture]
public class AuthorizationTests
{
    public enum BadAuth { Missing, Garbage, BasicScheme, Expired, WrongKey, WrongIssuer, WrongAudience, Unsigned }

    public static readonly string[] Endpoints =
    [
        "GET /api/tasks",
        "GET /api/tasks/summary",
        "GET /api/tasks/{id}",
        "POST /api/tasks",
        "PUT /api/tasks/{id}",
        "DELETE /api/tasks/{id}"
    ];

    private static ApiFactory Factory => TestApp.Factory;

    [Test]
    public async Task Missing_or_invalid_token_is_401_on_every_task_endpoint(
        [ValueSource(nameof(Endpoints))] string endpoint, [Values] BadAuth auth)
    {
        using var client = TestApp.AnonymousClient();
        using var request = BuildRequest(endpoint);
        request.Headers.Authorization = auth switch
        {
            BadAuth.Missing => null,
            BadAuth.Garbage => new AuthenticationHeaderValue("Bearer", "garbage"),
            BadAuth.BasicScheme => new AuthenticationHeaderValue("Basic", Convert.ToBase64String("u:p"u8.ToArray())),
            BadAuth.Expired => Bearer(Factory.CreateToken(
                notBefore: DateTime.UtcNow.AddHours(-3), expires: DateTime.UtcNow.AddHours(-2))),
            BadAuth.WrongKey => Bearer(Factory.CreateToken(signingKey: Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + Factory.SigningKey)),
            BadAuth.WrongIssuer => Bearer(Factory.CreateToken(issuer: "someone-else")),
            BadAuth.WrongAudience => Bearer(Factory.CreateToken(audience: "another-app")),
            BadAuth.Unsigned => Bearer(Factory.CreateToken(signed: false)),
            _ => throw new ArgumentOutOfRangeException(nameof(auth))
        };

        var response = await client.SendAsync(request);

        var problem = await ProblemAssert.IsProblem(response, HttpStatusCode.Unauthorized, ErrorCodes.Unauthorized);
        Assert.That(problem.GetProperty("detail").GetString(), Does.Contain("/api/auth/login"));
    }

    [Test]
    public async Task Valid_token_is_accepted_on_every_task_endpoint([ValueSource(nameof(Endpoints))] string endpoint)
    {
        using var client = TestApp.AuthorizedClient();
        using var request = BuildRequest(endpoint);

        var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(ExpectedWhenAuthorized(endpoint)), await response.Content.ReadAsStringAsync());
    }

    [Test]
    public async Task A_freshly_issued_test_token_is_accepted()
    {
        using var client = ApiFactory.WithToken(TestApp.AnonymousClient(), Factory.CreateToken());

        Assert.That((await client.GetAsync("/api/tasks")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Unknown_route_without_token_is_401_and_with_token_is_404()
    {
        using var anonymous = TestApp.AnonymousClient();
        using var authorized = TestApp.AuthorizedClient();

        await ProblemAssert.IsProblem(await anonymous.GetAsync("/api/does-not-exist"), HttpStatusCode.Unauthorized, ErrorCodes.Unauthorized);
        await ProblemAssert.IsProblem(await authorized.GetAsync("/api/does-not-exist"), HttpStatusCode.NotFound, ErrorCodes.RouteNotFound);
    }

    private static AuthenticationHeaderValue Bearer(string token) => new("Bearer", token);

    private static HttpRequestMessage BuildRequest(string endpoint)
    {
        var parts = endpoint.Split(' ');
        var url = parts[1].Replace("{id}", Guid.NewGuid().ToString());
        var request = new HttpRequestMessage(new HttpMethod(parts[0]), url);
        if (parts[0] is "POST" or "PUT")
        {
            // POST: invalid body -> 400 when authorized (nothing is created).
            // PUT: valid body for a random id -> 404 when authorized.
            request.Content = ProblemAssert.Json(parts[0] == "POST"
                ? "{}"
                : """{ "title": "x", "status": "ToDo", "priority": "Low" }""");
        }
        return request;
    }

    private static HttpStatusCode ExpectedWhenAuthorized(string endpoint) => endpoint switch
    {
        "GET /api/tasks" or "GET /api/tasks/summary" => HttpStatusCode.OK,
        "POST /api/tasks" => HttpStatusCode.BadRequest,
        _ => HttpStatusCode.NotFound
    };
}
