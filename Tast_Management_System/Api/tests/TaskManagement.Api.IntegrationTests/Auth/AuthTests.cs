using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using TaskManagement.Api.IntegrationTests.Infrastructure;
using TaskManagement.Application.Abstractions;
using static TaskManagement.Api.IntegrationTests.Infrastructure.ProblemAssert;

namespace TaskManagement.Api.IntegrationTests.Auth;

/// <summary>POST /api/auth/login: every positive and negative scenario (task.md T8a / T9).</summary>
[TestFixture]
public class AuthTests
{
    private const string Login = "/api/auth/login";
    private static ApiFactory Factory => TestApp.Factory;

    private HttpClient _client = null!;

    [SetUp]
    public void SetUp() => _client = TestApp.AnonymousClient();

    [TearDown]
    public void TearDown() => _client.Dispose();

    private Task<HttpResponseMessage> PostLogin(object body) => _client.PostAsJsonAsync(Login, body);

    // ---------- Positive ----------

    [Test]
    public async Task Valid_credentials_return_a_bearer_token_valid_for_60_minutes()
    {
        var before = DateTime.UtcNow;
        var response = await PostLogin(new { username = Factory.Username, password = Factory.Password });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("tokenType").GetString(), Is.EqualTo("Bearer"));
            Assert.That(body.GetProperty("username").GetString(), Is.EqualTo(Factory.Username));
            Assert.That(body.GetProperty("accessToken").GetString()!.Split('.'), Has.Length.EqualTo(3), "JWT has 3 parts");
            Assert.That(body.GetProperty("expiresAtUtc").GetString(), Does.EndWith("Z"));
            Assert.That(body.GetProperty("expiresAtUtc").GetDateTime(),
                Is.InRange(before.AddMinutes(59), DateTime.UtcNow.AddMinutes(61)));
        });
    }

    [Test]
    public async Task Username_is_case_insensitive_and_returned_as_configured()
    {
        var response = await PostLogin(new { username = Factory.Username.ToUpperInvariant(), password = Factory.Password });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("username").GetString(),
            Is.EqualTo(Factory.Username));
    }

    [Test]
    public async Task Token_contains_subject_issuer_audience_and_a_60_minute_lifetime()
    {
        var response = await PostLogin(new { username = Factory.Username, password = Factory.Password });
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var payload = DecodePayload(token);
        Assert.Multiple(() =>
        {
            Assert.That(payload.GetProperty("sub").GetString(), Is.EqualTo(Factory.Username));
            Assert.That(payload.GetProperty("email").GetString(), Is.EqualTo(Factory.Username));
            Assert.That(payload.GetProperty("iss").GetString(), Is.EqualTo(ApiFactory.Issuer));
            Assert.That(payload.GetProperty("aud").GetString(), Is.EqualTo(ApiFactory.Audience));
            Assert.That(payload.GetProperty("exp").GetInt64() - payload.GetProperty("iat").GetInt64(), Is.EqualTo(3600));
            Assert.That(payload.TryGetProperty("jti", out _), Is.True);
        });
    }

    [Test]
    public async Task One_token_works_for_every_task_api()
    {
        using var client = TestApp.AuthorizedClient();

        await Assert.MultipleAsync(async () =>
        {
            Assert.That((await client.GetAsync("/api/tasks")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That((await client.GetAsync("/api/tasks/summary")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That((await client.GetAsync($"/api/tasks/{Guid.NewGuid()}")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    [Test]
    public async Task Login_is_anonymous_even_when_a_broken_token_is_sent()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-token");

        var response = await PostLogin(new { username = Factory.Username, password = Factory.Password });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    // ---------- Negative: credentials ----------

    [Test]
    public async Task Wrong_password_is_rejected_with_invalid_credentials()
    {
        var response = await PostLogin(new { username = Factory.Username, password = "wrong-password" });

        var problem = await IsProblem(response, HttpStatusCode.Unauthorized, ErrorCodes.InvalidCredentials);
        Assert.That(problem.GetProperty("detail").GetString(), Is.EqualTo("Invalid username or password."));
    }

    [Test]
    public async Task Unknown_user_gets_exactly_the_same_response_as_a_wrong_password()
    {
        var unknown = await IsProblem(await PostLogin(new { username = "nobody@integration.test", password = Factory.Password }),
            HttpStatusCode.Unauthorized, ErrorCodes.InvalidCredentials);
        var wrong = await IsProblem(await PostLogin(new { username = Factory.Username, password = "wrong-password" }),
            HttpStatusCode.Unauthorized, ErrorCodes.InvalidCredentials);

        Assert.Multiple(() =>
        {
            Assert.That(unknown.GetProperty("title").GetString(), Is.EqualTo(wrong.GetProperty("title").GetString()));
            Assert.That(unknown.GetProperty("detail").GetString(), Is.EqualTo(wrong.GetProperty("detail").GetString()));
        });
    }

    [Test]
    public async Task Password_is_case_sensitive() =>
        await IsProblem(await PostLogin(new { username = Factory.Username, password = Factory.Password.ToUpperInvariant() }),
            HttpStatusCode.Unauthorized, ErrorCodes.InvalidCredentials);

    [Test]
    public async Task Configured_test_users_do_not_work_outside_development()
    {
        await using var production = Factory.WithWebHostBuilder(b => b.UseEnvironment("Production"));
        using var client = production.CreateClient();

        var response = await client.PostAsJsonAsync(Login, new { username = Factory.Username, password = Factory.Password });

        await IsProblem(response, HttpStatusCode.Unauthorized, ErrorCodes.InvalidCredentials);
    }

    [Test]
    public async Task Configured_test_users_work_outside_development_when_explicitly_allowed()
    {
        await using var production = Factory.WithWebHostBuilder(b => b
            .UseEnvironment("Production")
            .UseSetting("Auth:AllowConfiguredUsersOutsideDevelopment", "true"));
        using var client = production.CreateClient();

        var response = await client.PostAsJsonAsync(Login, new { username = Factory.Username, password = Factory.Password });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    // ---------- Negative: validation ----------

    [TestCase("""{ "password": "x" }""", "Username", "Username is required.")]
    [TestCase("""{ "username": "", "password": "x" }""", "Username", "Username is required.")]
    [TestCase("""{ "username": "   ", "password": "x" }""", "Username", "Username is required.")]
    [TestCase("""{ "username": "not-an-email", "password": "x" }""", "Username", "Username must be a valid email address.")]
    [TestCase("""{ "username": "a@b.test" }""", "Password", "Password is required.")]
    [TestCase("""{ "username": "a@b.test", "password": "" }""", "Password", "Password is required.")]
    public async Task Missing_or_invalid_fields_are_field_validation_errors(string json, string field, string message)
    {
        var response = await _client.PostAsync(Login, Json(json));

        var problem = await IsProblem(response, HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);
        Assert.That(FieldErrors(problem, field), Is.EqualTo(new[] { message }));
    }

    [Test]
    public async Task Username_longer_than_256_is_rejected()
    {
        var response = await PostLogin(new { username = new string('a', 251) + "@x.com", password = "x" });

        var problem = await IsProblem(response, HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);
        Assert.That(FieldErrors(problem, "Username"), Is.EqualTo(new[] { "Username cannot exceed 256 characters." }));
    }

    [Test]
    public async Task Password_longer_than_128_is_rejected_without_echoing_it()
    {
        var password = "P" + new string('w', 128);
        var response = await PostLogin(new { username = Factory.Username, password });

        var problem = await IsProblem(response, HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);
        Assert.Multiple(() =>
        {
            Assert.That(FieldErrors(problem, "Password"), Is.EqualTo(new[] { "Password cannot exceed 128 characters." }));
            Assert.That(problem.GetRawText(), Does.Not.Contain(password));
        });
    }

    [Test]
    public async Task Malformed_json_is_an_invalid_request() =>
        await IsProblem(await _client.PostAsync(Login, Json("""{ "username": """)), HttpStatusCode.BadRequest, ErrorCodes.RequestInvalid);

    [Test]
    public async Task Non_json_content_type_is_unsupported_media_type() =>
        await IsProblem(await _client.PostAsync(Login, new StringContent("username=a", Encoding.UTF8, "text/plain")),
            HttpStatusCode.UnsupportedMediaType, ErrorCodes.UnsupportedMediaType);

    [Test]
    public async Task Get_on_login_is_401_without_a_token_and_405_with_one()
    {
        // Only POST /api/auth/login is anonymous; any other request (even a wrong method) needs a token.
        using var authorized = TestApp.AuthorizedClient();

        await IsProblem(await _client.GetAsync(Login), HttpStatusCode.Unauthorized, ErrorCodes.Unauthorized);
        await IsProblem(await authorized.GetAsync(Login), HttpStatusCode.MethodNotAllowed, ErrorCodes.MethodNotAllowed);
    }

    // ---------- Never leak the password ----------

    [Test]
    public async Task Password_never_appears_in_responses_or_logs()
    {
        var wrongPassword = $"Wrong-{Guid.NewGuid():N}";
        var failed = await PostLogin(new { username = Factory.Username, password = wrongPassword });
        var succeeded = await PostLogin(new { username = Factory.Username, password = Factory.Password });

        var logs = string.Join('\n', Factory.Logs.Messages);
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await failed.Content.ReadAsStringAsync(), Does.Not.Contain(wrongPassword));
            Assert.That(await succeeded.Content.ReadAsStringAsync(), Does.Not.Contain(Factory.Password));
            Assert.That(logs, Does.Not.Contain(wrongPassword));
            Assert.That(logs, Does.Not.Contain(Factory.Password));
            Assert.That(logs, Does.Contain($"Login failed for {Factory.Username}"), "failures are logged with the username");
        });
    }

    private static JsonElement DecodePayload(string jwt)
    {
        var part = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        part = part.PadRight(part.Length + (4 - part.Length % 4) % 4, '=');
        return JsonDocument.Parse(Convert.FromBase64String(part)).RootElement;
    }
}
