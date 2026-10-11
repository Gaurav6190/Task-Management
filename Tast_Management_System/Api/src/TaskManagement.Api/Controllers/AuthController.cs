using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Api.Contracts.Auth;
using TaskManagement.Api.Infrastructure;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Auth.Login;

namespace TaskManagement.Api.Controllers;

[ApiController]
[Route("api/auth")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
public sealed class AuthController : ControllerBase
{
    /// <summary>
    /// Exchanges a username (email) and password for a JWT access token, valid for 60 minutes.
    /// Log in once and send the token as "Authorization: Bearer {accessToken}" on every task API call.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        [FromServices] ICommandHandler<LoginCommand, LoginResult> handler,
        CancellationToken ct)
    {
        var result = await handler.Handle(new LoginCommand(request.Username, request.Password), ct);
        if (result.IsFailure)
            return result.ToProblem(this);

        var login = result.Value;
        return Ok(new LoginResponse(login.AccessToken, login.TokenType, login.ExpiresAtUtc, login.Username));
    }
}
