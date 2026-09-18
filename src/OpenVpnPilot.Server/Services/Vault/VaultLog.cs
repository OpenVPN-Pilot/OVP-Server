namespace OpenVpnPilot.Server.Services.Vault;

// Who read or changed which sign in is logged; the sign in itself never is.
internal static partial class VaultLog
{
    [LoggerMessage(EventId = 3100, Level = LogLevel.Information, Message = "{User} read {Count} vault entr(ies) of profile {ProfileId} (empty means every profile)")]
    public static partial void Read(ILogger logger, string user, int count, Guid? profileId);

    [LoggerMessage(EventId = 3101, Level = LogLevel.Information, Message = "{User} added the vault entry {Realm} of profile {ProfileId}, change {ChangeSeq}")]
    public static partial void Added(ILogger logger, string user, Guid profileId, string realm, long changeSeq);

    [LoggerMessage(EventId = 3102, Level = LogLevel.Information, Message = "{User} replaced the vault entry {Realm} of profile {ProfileId}, change {ChangeSeq}")]
    public static partial void Replaced(ILogger logger, string user, Guid profileId, string realm, long changeSeq);

    [LoggerMessage(EventId = 3103, Level = LogLevel.Information, Message = "{User} deleted the vault entry {Realm} of profile {ProfileId}, change {ChangeSeq}")]
    public static partial void Deleted(ILogger logger, string user, Guid profileId, string realm, long changeSeq);
}
