using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TaskManagement.Infrastructure.Auth;

namespace TaskManagement.Api.Infrastructure;

/// <summary>JWT bearer authentication, secure-by-default authorization, CORS for the UI, and Swagger auth.</summary>
public static class AuthSetup
{
    public const string UiCorsPolicy = "Ui";

    public static IServiceCollection AddApiAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Configured from JwtOptions at runtime, so the same validated options issue and validate tokens.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                bearer.MapInboundClaims = false;    // keep "sub"/"email" as-is
                bearer.IncludeErrorDetails = false; // don't describe why a token failed in WWW-Authenticate
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = JwtSigningKey.Create(jwt.SigningKey),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = "sub"
                };
            });

        // Every endpoint requires a valid token unless it is marked [AllowAnonymous] (only login is).
        services.AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build());

        // TEMPORARY configured test users are Development-only, so a well-known test login can never
        // work on a deployed server, even if someone sets Auth:Users there by mistake. The only exception is
        // an explicit Auth:AllowConfiguredUsersOutsideDevelopment=true (localhost Docker deployment).
        // Checked when the options are first used (not at registration), so it always sees the real environment.
        services.AddOptions<AuthOptions>().PostConfigure<IHostEnvironment>((auth, env) =>
        {
            if (!env.IsDevelopment() && !auth.AllowConfiguredUsersOutsideDevelopment)
                auth.Users.Clear();
        });

        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(o => o.AddPolicy(UiCorsPolicy, policy => policy
            .WithOrigins(origins)
            .WithMethods("GET", "POST", "PUT", "DELETE")
            .WithHeaders("Authorization", "Content-Type")));

        return services;
    }

    /// <summary>Adds the "Authorize" button to Swagger UI so endpoints can be tried with a token.</summary>
    public static void AddBearerSecurity(this Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions swagger)
    {
        var scheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste the accessToken from POST /api/auth/login.",
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        };
        swagger.AddSecurityDefinition("Bearer", scheme);
        swagger.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = [] });
    }
}
