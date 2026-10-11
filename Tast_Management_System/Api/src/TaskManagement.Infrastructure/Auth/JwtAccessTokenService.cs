using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TaskManagement.Application.Auth;

namespace TaskManagement.Infrastructure.Auth;

/// <summary>Issues HMAC-SHA256 signed JWT access tokens (60 minutes by default, no refresh token).</summary>
internal sealed class JwtAccessTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : IAccessTokenService
{
    public AccessToken Issue(AuthenticatedUser user)
    {
        var jwt = options.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(jwt.AccessTokenMinutes);

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Username),
                new Claim(JwtRegisteredClaimNames.Email, user.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ]),
            SigningCredentials = new SigningCredentials(JwtSigningKey.Create(jwt.SigningKey), SecurityAlgorithms.HmacSha256)
        });

        return new AccessToken(token, expires);
    }
}

/// <summary>Shared by token issuing (here) and validation (Api) so both always use the same key.</summary>
public static class JwtSigningKey
{
    public static SymmetricSecurityKey Create(string signingKey) => new(Encoding.UTF8.GetBytes(signingKey));
}
