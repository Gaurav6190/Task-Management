using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TaskManagement.Application.Auth;
using TaskManagement.Infrastructure.Auth;

namespace TaskManagement.Infrastructure.UnitTests.Auth;

[TestFixture]
public class JwtAccessTokenServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 11, 9, 0, 0, DateTimeKind.Utc);
    private static readonly string Key = Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + Guid.NewGuid().ToString("N");

    private static JwtOptions Options(int minutes = 60) => new()
    {
        Issuer = "TaskManagement.Api", Audience = "TaskManagement.Ui", AccessTokenMinutes = minutes, SigningKey = Key
    };

    private static AccessToken Issue(JwtOptions? options = null) =>
        new JwtAccessTokenService(Microsoft.Extensions.Options.Options.Create(options ?? Options()), new FixedTime(Now))
            .Issue(new AuthenticatedUser("tester@example.com"));

    private static Task<TokenValidationResult> Validate(string token, string key) =>
        new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = "TaskManagement.Api",
            ValidAudience = "TaskManagement.Ui",
            IssuerSigningKey = JwtSigningKey.Create(key),
            ValidateLifetime = false // fixed clock in the past; lifetime is asserted separately
        });

    [Test]
    public void Token_expires_after_the_configured_minutes()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Issue().ExpiresAtUtc, Is.EqualTo(Now.AddMinutes(60)));
            Assert.That(Issue(Options(15)).ExpiresAtUtc, Is.EqualTo(Now.AddMinutes(15)));
        });
    }

    [Test]
    public void Token_contains_the_expected_claims()
    {
        var jwt = new JsonWebToken(Issue().Token);

        Assert.Multiple(() =>
        {
            Assert.That(jwt.Alg, Is.EqualTo(SecurityAlgorithms.HmacSha256));
            Assert.That(jwt.Subject, Is.EqualTo("tester@example.com"));
            Assert.That(jwt.GetClaim("email").Value, Is.EqualTo("tester@example.com"));
            Assert.That(Guid.TryParse(jwt.Id, out _), Is.True, "jti is a GUID");
            Assert.That(jwt.Issuer, Is.EqualTo("TaskManagement.Api"));
            Assert.That(jwt.Audiences, Is.EqualTo(new[] { "TaskManagement.Ui" }));
            Assert.That(jwt.IssuedAt, Is.EqualTo(Now));
            Assert.That(jwt.ValidFrom, Is.EqualTo(Now));
            Assert.That(jwt.ValidTo, Is.EqualTo(Now.AddMinutes(60)));
        });
    }

    [Test]
    public void Every_token_gets_a_unique_id()
    {
        var first = new JsonWebToken(Issue().Token).Id;
        var second = new JsonWebToken(Issue().Token).Id;

        Assert.That(first, Is.Not.EqualTo(second));
    }

    [Test]
    public async Task Token_validates_with_the_same_key()
    {
        var result = await Validate(Issue().Token, Key);

        Assert.That(result.IsValid, Is.True, result.Exception?.Message);
    }

    [Test]
    public async Task Token_does_not_validate_with_another_key()
    {
        var result = await Validate(Issue().Token, Key + "-different");

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void JwtSigningKey_uses_the_utf8_bytes_of_the_key() =>
        Assert.That(JwtSigningKey.Create("abc").Key, Is.EqualTo("abc"u8.ToArray()));

    private sealed class FixedTime(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
