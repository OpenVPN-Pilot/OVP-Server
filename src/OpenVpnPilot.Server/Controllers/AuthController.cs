using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenVpnPilot.Server.Auth;
using OpenVpnPilot.Server.Contracts.Requests;
using OpenVpnPilot.Server.Contracts.Responses;
using OpenVpnPilot.Server.Middleware;
using OpenVpnPilot.Server.Services.Auth;

namespace OpenVpnPilot.Server.Controllers;

/// <summary>Signing in, keeping the session alive and signing out.</summary>
[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public sealed class AuthController(IAuthService auth, ISessionService sessions, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Signs in with a user name and, except in mode <c>none</c>, a password.</summary>
    /// <remarks>
    /// Refused with <c>auth.mode_mismatch</c> when the server signs in with Entra ID. A correct password for an account
    /// that is disabled answers 401 with <c>account.revoked</c> and the wipe directive.
    /// </remarks>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthorizationPolicies.SignInRateLimit)]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public Task<TokenResponse> Login(LoginRequest request, CancellationToken cancellationToken) =>
        auth.LoginAsync(request, HttpContext.GetClientContext().ClientId, cancellationToken);

    /// <summary>Exchanges an Entra ID access token for this server's tokens.</summary>
    /// <remarks>Only in mode <c>entra</c>. Obtain the token with the tenant, client id and scope from <c>GET /api/v1/server/info</c>.</remarks>
    [HttpPost("entra/exchange")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthorizationPolicies.SignInRateLimit)]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public Task<TokenResponse> ExchangeEntra(EntraExchangeRequest request, CancellationToken cancellationToken) =>
        auth.ExchangeEntraAsync(request, HttpContext.GetClientContext().ClientId, cancellationToken);

    /// <summary>Trades the refresh token for a new access token and a new refresh token.</summary>
    /// <remarks>
    /// The refresh token sent is used up. Presenting it a second time ends the whole session with
    /// <c>auth.refresh_token_reused</c>, so a client must never refresh twice in parallel.
    /// </remarks>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthorizationPolicies.SignInRateLimit)]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public Task<TokenResponse> Refresh(RefreshRequest request, CancellationToken cancellationToken) =>
        sessions.RefreshAsync(request.RefreshToken, HttpContext.GetClientContext().ClientId, cancellationToken);

    /// <summary>Ends the session of this installation.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<NoContentResult> Logout(LogoutRequest request, CancellationToken cancellationToken)
    {
        await sessions.LogoutAsync(request.RefreshToken, HttpContext.GetClientContext().ClientId, cancellationToken);
        return NoContent();
    }

    /// <summary>The signed in user.</summary>
    [HttpGet("me")]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    public Task<CurrentUserResponse> Me(CancellationToken cancellationToken) =>
        auth.MeAsync(currentUser.Id, cancellationToken);
}
