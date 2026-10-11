using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaskManagement.Infrastructure.Auth;

namespace TaskManagement.Infrastructure.UnitTests.Auth;

[TestFixture]
public class ConfiguredUserAuthenticatorTests
{
    private const string Username = "tester@example.com";
    private static readonly string Password = $"pw-{Guid.NewGuid():N}"; // generated per run, never a real credential

    private static ConfiguredUserAuthenticator Create(params ConfiguredUser[] users) =>
        new(new StaticOptionsMonitor<AuthOptions>(new AuthOptions { Users = [.. users] }),
            NullLogger<ConfiguredUserAuthenticator>.Instance);

    private static ConfiguredUser User(string username = Username, string? password = null) => new()
    {
        Username = username,
        PasswordHash = new PasswordHasher<ConfiguredUser>().HashPassword(new ConfiguredUser(), password ?? Password)
    };

    [Test]
    public async Task Correct_credentials_return_the_user()
    {
        var user = await Create(User()).AuthenticateAsync(Username, Password, CancellationToken.None);

        Assert.That(user?.Username, Is.EqualTo(Username));
    }

    [Test]
    public async Task Username_is_case_insensitive_and_the_configured_spelling_is_returned()
    {
        var user = await Create(User("Tester@Example.com")).AuthenticateAsync("TESTER@EXAMPLE.COM", Password, CancellationToken.None);

        Assert.That(user?.Username, Is.EqualTo("Tester@Example.com"));
    }

    [Test]
    public async Task The_right_user_is_picked_when_several_are_configured()
    {
        var user = await Create(User("a@example.com", "pa"), User("b@example.com", "pb"))
            .AuthenticateAsync("b@example.com", "pb", CancellationToken.None);

        Assert.That(user?.Username, Is.EqualTo("b@example.com"));
    }

    [TestCase("wrong")]
    [TestCase("")]
    public async Task Wrong_password_returns_null(string password) =>
        Assert.That(await Create(User()).AuthenticateAsync(Username, password, CancellationToken.None), Is.Null);

    [Test]
    public async Task Password_is_case_sensitive() =>
        Assert.That(await Create(User()).AuthenticateAsync(Username, Password.ToUpperInvariant(), CancellationToken.None), Is.Null);

    [Test]
    public async Task Unknown_user_returns_null() =>
        Assert.That(await Create(User()).AuthenticateAsync("nobody@example.com", Password, CancellationToken.None), Is.Null);

    [Test]
    public async Task No_configured_users_means_nobody_can_log_in() =>
        Assert.That(await Create().AuthenticateAsync(Username, Password, CancellationToken.None), Is.Null);

    [TestCase("not-a-hash")]
    [TestCase("")]
    public async Task Malformed_configured_hash_returns_null_instead_of_throwing(string hash)
    {
        var authenticator = Create(new ConfiguredUser { Username = Username, PasswordHash = hash });

        Assert.That(await authenticator.AuthenticateAsync(Username, Password, CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task Hash_produced_by_the_dev_script_format_is_accepted()
    {
        // scripts/Set-DevAuthUser.ps1 writes Identity V3 (PBKDF2-HMACSHA512, 100k iterations): the same
        // format PasswordHasher produces by default in .NET 8. Verify that default explicitly.
        var hash = new PasswordHasher<ConfiguredUser>().HashPassword(new ConfiguredUser(), Password);
        var bytes = Convert.FromBase64String(hash);

        Assert.Multiple(() =>
        {
            Assert.That(bytes[0], Is.EqualTo(0x01), "format marker V3");
            Assert.That(ReadUInt32BigEndian(bytes, 1), Is.EqualTo(2u), "PRF = HMACSHA512");
            Assert.That(ReadUInt32BigEndian(bytes, 5), Is.EqualTo(100_000u), "iterations");
            Assert.That(ReadUInt32BigEndian(bytes, 9), Is.EqualTo(16u), "salt length");
        });
        Assert.That(await Create(new ConfiguredUser { Username = Username, PasswordHash = hash })
            .AuthenticateAsync(Username, Password, CancellationToken.None), Is.Not.Null);
    }

    private static uint ReadUInt32BigEndian(byte[] b, int offset) =>
        (uint)(b[offset] << 24 | b[offset + 1] << 16 | b[offset + 2] << 8 | b[offset + 3]);

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
