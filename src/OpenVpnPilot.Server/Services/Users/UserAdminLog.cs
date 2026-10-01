using OpenVpnPilot.Server.Data.Entities;

namespace OpenVpnPilot.Server.Services.Users;

internal static partial class UserAdminLog
{
    [LoggerMessage(EventId = 4000, Level = LogLevel.Warning, Message = "{Admin} changed user {Username} from {From} to {To}")]
    public static partial void StateChanged(ILogger logger, string admin, string username, UserState from, UserState to);

    [LoggerMessage(EventId = 4001, Level = LogLevel.Warning, Message = "{Admin} purged user {Username} and everything stored for them")]
    public static partial void Purged(ILogger logger, string admin, string username);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Information, Message = "{Admin} signed out user {Username} everywhere, {Count} token(s) revoked")]
    public static partial void TokensRevoked(ILogger logger, string admin, string username, int count);
}
