using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UbbEvents.Api.DTOs;
using UbbEvents.Api.Services;

namespace UbbEvents.Api.Controllers;

[ApiController, Route("api/auth")]
[ProducesResponseType<ProblemDetails>(400)]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    /// <summary>Create a student account. Organization and global roles cannot be selected.</summary>
    [HttpPost("register"), AllowAnonymous, EnableRateLimiting("auth")]
    [ProducesResponseType<ProblemDetails>(429)]
    [ProducesResponseType<AuthResponse>(201), ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(409)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
        => Created("/api/auth/me", await auth.RegisterAsync(request, ct));

    /// <summary>Exchange credentials for a short-lived bearer token.</summary>
    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("auth")]
    [ProducesResponseType<ProblemDetails>(429)]
    [ProducesResponseType<AuthResponse>(200), ProducesResponseType<ProblemDetails>(401)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
        => Ok(await auth.LoginAsync(request, ct));

    /// <summary>Retrieve the authenticated user and their organization roles.</summary>
    [HttpGet("me"), Authorize]
    [ProducesResponseType<CurrentUserResponse>(200), ProducesResponseType<ProblemDetails>(401)]
    public async Task<ActionResult<CurrentUserResponse>> Me(CancellationToken ct) => Ok(await auth.MeAsync(ct));
}
