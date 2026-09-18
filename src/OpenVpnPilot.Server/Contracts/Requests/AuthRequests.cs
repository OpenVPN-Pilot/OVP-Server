using System.ComponentModel.DataAnnotations;

namespace OpenVpnPilot.Server.Contracts.Requests;

/// <summary>Signs in with a user name, and a password unless the server runs in mode <c>none</c>.</summary>
/// <param name="Username">The user name. In mode <c>ldap</c> this is what the configured filter matches, usually the account name.</param>
/// <param name="Password">The password. Ignored in mode <c>none</c>, required in <c>file</c> and <c>ldap</c>.</param>
public sealed record LoginRequest(
    [Required, MaxLength(256)] string Username,
    [MaxLength(1024)] string? Password);

/// <summary>Exchanges an Entra ID access token for this server's own tokens.</summary>
/// <param name="AccessToken">An access token issued by Entra ID for the scope published in <c>GET /api/v1/server/info</c>.</param>
public sealed record EntraExchangeRequest(
    [Required, MaxLength(16384)] string AccessToken);

/// <summary>Trades a refresh token for a new pair. The refresh token sent is used up.</summary>
/// <param name="RefreshToken">The refresh token from the last sign in or refresh.</param>
public sealed record RefreshRequest(
    [Required, MaxLength(512)] string RefreshToken);

/// <summary>Ends the session the refresh token belongs to, on this installation only.</summary>
/// <param name="RefreshToken">The current refresh token.</param>
public sealed record LogoutRequest(
    [Required, MaxLength(512)] string RefreshToken);
