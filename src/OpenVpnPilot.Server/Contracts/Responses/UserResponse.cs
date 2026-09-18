namespace OpenVpnPilot.Server.Contracts.Responses;

/// <summary>A user as an administrator sees it.</summary>
/// <param name="Id">Stable identifier.</param>
/// <param name="Username">The user name.</param>
/// <param name="DisplayName">A name to show, when the identity provider has one.</param>
/// <param name="Role"><c>admin</c> or <c>user</c>, as the identity provider reported it at the last sign in or refresh.</param>
/// <param name="Provider"><c>none</c>, <c>file</c>, <c>ldap</c> or <c>entra</c>.</param>
/// <param name="State"><c>active</c>, <c>disabled</c> or <c>deleted</c>. A client of a disabled or deleted user is told to wipe itself.</param>
/// <param name="StateSource">Who set a disabled or deleted state: <c>administrator</c>, or <c>provider</c> when the identity provider reported it.</param>
/// <param name="StateChangedAt">When the state last changed.</param>
/// <param name="CreatedAt">When the user first signed in.</param>
/// <param name="LastLoginAt">The last sign in.</param>
/// <param name="LastSeenAt">The last request, to within a few minutes.</param>
public sealed record UserResponse(
    Guid Id,
    string Username,
    string? DisplayName,
    string Role,
    string Provider,
    string State,
    string? StateSource,
    DateTimeOffset? StateChangedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset? LastSeenAt);
