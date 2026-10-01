using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Opdeweg.Api.Configuration;
using Opdeweg.Api.Extensions;
using Opdeweg.Application.DTOs;
using Opdeweg.Application.Features.Auth;

namespace Opdeweg.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.Auth)]
    public Task<AuthResponse> Register(RegisterRequest request, CancellationToken cancellationToken) =>
        auth.RegisterAsync(request, cancellationToken);

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.Auth)]
    public Task<AuthResponse> Login(LoginRequest request, CancellationToken cancellationToken) =>
        auth.LoginAsync(request, cancellationToken);

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.Auth)]
    public Task<AuthResponse> Refresh(RefreshRequest request, CancellationToken cancellationToken) =>
        auth.RefreshAsync(request, cancellationToken);

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken cancellationToken)
    {
        await auth.LogoutAsync(User.GetUserId(), request, cancellationToken);
        return NoContent();
    }
}
