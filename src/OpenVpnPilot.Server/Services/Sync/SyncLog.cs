namespace OpenVpnPilot.Server.Services.Sync;

internal static partial class SyncLog
{
    [LoggerMessage(EventId = 3200, Level = LogLevel.Information,
        Message = "{User} synchronised from {Since} to {Cursor}: {Profiles} profile(s), {Vault} vault entr(ies), {Deletions} deletion(s)")]
    public static partial void Answered(ILogger logger, string user, long since, long cursor, int profiles, int vault, int deletions);

    [LoggerMessage(EventId = 3201, Level = LogLevel.Information,
        Message = "{User} asked for changes since {Since}, which cannot be answered (pruned through {PrunedThrough}, current {Cursor})")]
    public static partial void CursorExpired(ILogger logger, string user, long since, long prunedThrough, long cursor);
}
