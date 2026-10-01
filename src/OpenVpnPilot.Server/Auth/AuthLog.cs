using OpenVpnPilot.Server.Data.Entities;

namespace OpenVpnPilot.Server.Auth;

internal static partial class AuthLog
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Signed in {Username} as {Role} on client {ClientId}")]
    public static partial void SignedIn(ILogger logger, string username, UserRole role, Guid clientId);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Warning, Message = "Refused sign in for {Username} on client {ClientId}: {Code}")]
    public static partial void SignInRefused(ILogger logger, string username, Guid clientId, string code);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Created user {Username}, first signed in through {Provider}")]
    public static partial void UserCreated(ILogger logger, string username, AuthProviderKind provider);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Information, Message = "Role of {Username} changed from {From} to {To}; existing tokens are revoked")]
    public static partial void RoleChanged(ILogger logger, string username, UserRole from, UserRole to);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Warning, Message = "Identity provider changed {Username} from {From} to {To}")]
    public static partial void ProviderChangedState(ILogger logger, string username, UserState from, UserState to);

    [LoggerMessage(EventId = 2005, Level = LogLevel.Information, Message = "User {From} was renamed at the identity provider to {To}")]
    public static partial void UserRenamed(ILogger logger, string from, string to);

    [LoggerMessage(EventId = 2006, Level = LogLevel.Warning,
        Message = "Refused {Username}: the name belongs to Entra object {Stored}, the token names object {Presented}")]
    public static partial void IdentityConflict(ILogger logger, string username, string? stored, string? presented);

    [LoggerMessage(EventId = 2007, Level = LogLevel.Warning,
        Message = "Authentication mode none: anyone who reaches this server can sign in under any name, including {Admins}")]
    public static partial void NoAuthentication(ILogger logger, IReadOnlySet<string> admins);

    [LoggerMessage(EventId = 2010, Level = LogLevel.Information, Message = "Loaded {Count} user(s) from {Path}")]
    public static partial void UserFileLoaded(ILogger logger, string path, int count);

    [LoggerMessage(EventId = 2011, Level = LogLevel.Error, Message = "Kept the previous user list: {Path} could not be read or is not valid")]
    public static partial void UserFileRejected(ILogger logger, string path, Exception exception);

    [LoggerMessage(EventId = 2012, Level = LogLevel.Warning,
        Message = "User {Username} has a plain password in the user file. Replace it with the output of 'hash-password'")]
    public static partial void PlainPasswordInFile(ILogger logger, string username);

    [LoggerMessage(EventId = 2020, Level = LogLevel.Error, Message = "The directory at {Host}:{Port} cannot be reached or refused the service account")]
    public static partial void DirectoryUnavailable(ILogger logger, string host, int port, Exception exception);

    [LoggerMessage(EventId = 2021, Level = LogLevel.Error,
        Message = "{Variable} names the group {GroupDn}, which the directory does not have; sign in is refused until it is fixed")]
    public static partial void DirectoryGroupMissing(ILogger logger, string variable, string groupDn);

    [LoggerMessage(EventId = 2022, Level = LogLevel.Error,
        Message = "The user filter matches {Count} entries for {Username}; the account is neither signed in nor changed until the filter is unambiguous")]
    public static partial void DirectoryAmbiguousUser(ILogger logger, string username, int count);

    [LoggerMessage(EventId = 2023, Level = LogLevel.Debug, Message = "Skipped a search reference below {BaseDn}")]
    public static partial void ReferralSkipped(ILogger logger, string baseDn);

    [LoggerMessage(EventId = 2030, Level = LogLevel.Warning, Message = "Refused an Entra ID token: {Reason}")]
    public static partial void EntraTokenRejected(ILogger logger, string reason);

    [LoggerMessage(EventId = 2032, Level = LogLevel.Warning,
        Message = "Refused an Entra ID token requested by application {RequestedBy}, not by {ClientId}")]
    public static partial void EntraWrongApplication(ILogger logger, string requestedBy, string clientId);

    [LoggerMessage(EventId = 2031, Level = LogLevel.Error, Message = "Could not fetch the Entra ID signing keys of tenant {TenantId}")]
    public static partial void EntraMetadataUnavailable(ILogger logger, string tenantId, Exception exception);

    [LoggerMessage(EventId = 2100, Level = LogLevel.Debug, Message = "Refreshed the session of {Username} on client {ClientId}")]
    public static partial void Refreshed(ILogger logger, string username, Guid clientId);

    [LoggerMessage(EventId = 2101, Level = LogLevel.Information, Message = "Signed out {Username} on client {ClientId}, {Count} token(s) revoked")]
    public static partial void SignedOut(ILogger logger, string username, Guid clientId, int count);

    [LoggerMessage(EventId = 2107, Level = LogLevel.Information,
        Message = "Ignored a sign out from client {ClientId}: the refresh token is unknown ({Unknown}) or belongs to another client")]
    public static partial void SignOutIgnored(ILogger logger, Guid clientId, bool unknown);

    [LoggerMessage(EventId = 2102, Level = LogLevel.Warning,
        Message = "A refresh token of {Username} was used a second time from client {ClientId}; revoked {Count} token(s) of that session")]
    public static partial void RefreshTokenReused(ILogger logger, string username, Guid clientId, int count);

    [LoggerMessage(EventId = 2103, Level = LogLevel.Warning,
        Message = "A token of {Username} issued to client {Expected} was presented by client {Actual}")]
    public static partial void ClientMismatch(ILogger logger, string username, object? expected, Guid actual);

    [LoggerMessage(EventId = 2104, Level = LogLevel.Warning,
        Message = "Refused {Username}, whose account is {State} (set by {Source}); sent the wipe directive")]
    public static partial void RevokedUserRefused(ILogger logger, string username, UserState state, StateSource? source);

    [LoggerMessage(EventId = 2105, Level = LogLevel.Warning,
        Message = "A token for user {UserId}, who no longer exists, was presented; sent the wipe directive")]
    public static partial void UnknownUserRefused(ILogger logger, Guid userId);

    [LoggerMessage(EventId = 2106, Level = LogLevel.Information, Message = "Refused an access token of {Username} that was revoked by a change to the account")]
    public static partial void StaleTokenRefused(ILogger logger, string username);
}
