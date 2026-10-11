using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TaskManagement.Api.IntegrationTests.Infrastructure;

// Root namespace on purpose: a [SetUpFixture] applies to its namespace and everything below it.
namespace TaskManagement.Api.IntegrationTests;

/// <summary>
/// Starts the API once for the whole test run and logs in once (task.md D15: one login, one token
/// reused for every call). NUnit runs these tests sequentially, so database tests never interfere.
/// </summary>
[SetUpFixture]
public sealed class TestApp
{
    public static ApiFactory Factory { get; private set; } = null!;
    public static string AccessToken { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        Factory = new ApiFactory();
        using var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { username = Factory.Username, password = Factory.Password });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), "test login failed; check Auth/Jwt setup");
        AccessToken = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
    }

    [OneTimeTearDown]
    public async Task StopAsync() => await Factory.DisposeAsync();

    /// <summary>Client that sends the shared access token.</summary>
    public static HttpClient AuthorizedClient() => ApiFactory.WithToken(Factory.CreateClient(), AccessToken);

    /// <summary>Client without any Authorization header.</summary>
    public static HttpClient AnonymousClient() => Factory.CreateClient();
}
