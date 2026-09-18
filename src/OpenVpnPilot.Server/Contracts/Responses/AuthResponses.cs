namespace OpenVpnPilot.Server.Contracts.Responses;

/// <summary>A signed in session: a short lived access token and a long lived refresh token.</summary>
/// <param name="AccessToken">Sent as <c>Authorization: Bearer</c> on every request.</param>
/// <param name="AccessTokenExpiresAt">When the access token stops working. Refresh shortly before.</param>
/// <param name="RefreshToken">Single use. Store it in the operating system keystore, never in a plain file.</param>
/// <param name="RefreshTokenExpiresAt">When the refresh token stops working and the user has to sign in again.</param>
/// <param name="User">Who is signed in.</param>
public sealed record TokenResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    CurrentUserResponse User);

/// <summary>The signed in user.</summary>
/// <param name="Id">Stable identifier of the user on this server.</param>
/// <param name="Username">The user name.</param>
/// <param name="DisplayName">A name to show, when the identity provider has one.</param>
/// <param name="Role"><c>admin</c> or <c>user</c>. Admins may change profiles, tags and the vault, and manage users.</param>
/// <param name="Provider">How the user signed in: <c>none</c>, <c>file</c>, <c>ldap</c> or <c>entra</c>.</param>
public sealed record CurrentUserResponse(
    Guid Id,
    string Username,
    string? DisplayName,
    string Role,
    string Provider);

/// <summary>What a client needs to know before signing in.</summary>
/// <param name="Name">Always <c>OpenVPN Pilot Server</c>, so a client can tell it reached the right thing.</param>
/// <param name="Version">The server version.</param>
/// <param name="ApiVersion">The API version this server speaks, sent back in <c>X-Pilot-Api-Version</c>.</param>
/// <param name="MinimumClientVersion">Clients older than this are refused with <c>pilot.client_outdated</c>.</param>
/// <param name="AuthMode"><c>none</c>, <c>file</c>, <c>ldap</c> or <c>entra</c>. Decides which sign in the client shows.</param>
/// <param name="PasswordRequired">Whether <c>POST /api/v1/auth/login</c> needs a password.</param>
/// <param name="Entra">How to reach Entra ID, only in mode <c>entra</c>.</param>
public sealed record ServerInfoResponse(
    string Name,
    string Version,
    string ApiVersion,
    string MinimumClientVersion,
    string AuthMode,
    bool PasswordRequired,
    EntraInfoResponse? Entra);

/// <summary>What a client passes to the Microsoft identity platform to obtain a token for this server.</summary>
/// <param name="TenantId">The directory to sign in to.</param>
/// <param name="ClientId">The application registration the client signs in as, a public client with PKCE.</param>
/// <param name="Scope">The scope to request. Its access token is what <c>POST /api/v1/auth/entra/exchange</c> takes.</param>
/// <param name="Authority">The authority URL, for libraries that want one.</param>
public sealed record EntraInfoResponse(
    string TenantId,
    string ClientId,
    string Scope,
    string Authority);
