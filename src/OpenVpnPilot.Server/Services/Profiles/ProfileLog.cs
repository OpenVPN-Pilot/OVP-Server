namespace OpenVpnPilot.Server.Services.Profiles;

internal static partial class ProfileLog
{
    [LoggerMessage(EventId = 3000, Level = LogLevel.Information, Message = "{User} created profile {Name} ({ProfileId}), change {ChangeSeq}")]
    public static partial void Created(ILogger logger, string user, string name, Guid profileId, long changeSeq);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Information,
        Message = "{User} imported a batch of {Total}: {Created} created, {Duplicates} duplicate, {Rejected} rejected")]
    public static partial void BatchImported(ILogger logger, string user, int total, int created, int duplicates, int rejected);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Information,
        Message = "{User} updated profile {Name} ({ProfileId}), configuration changed: {ConfigurationChanged}, change {ChangeSeq}")]
    public static partial void Updated(ILogger logger, string user, string name, Guid profileId, bool configurationChanged, long changeSeq);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Information, Message = "{User} deleted profile {Name} ({ProfileId}), change {ChangeSeq}")]
    public static partial void Deleted(ILogger logger, string user, string name, Guid profileId, long changeSeq);

    [LoggerMessage(EventId = 3004, Level = LogLevel.Information, Message = "{User} fetched the configuration of profile {Name} ({ProfileId})")]
    public static partial void ConfigurationRead(ILogger logger, string user, string name, Guid profileId);

    [LoggerMessage(EventId = 3005, Level = LogLevel.Debug, Message = "{User} listed {Count} profile(s)")]
    public static partial void Listed(ILogger logger, string user, int count);

    [LoggerMessage(EventId = 3010, Level = LogLevel.Information, Message = "{User} created tag {Name}, change {ChangeSeq}")]
    public static partial void TagCreated(ILogger logger, string user, string name, long changeSeq);

    [LoggerMessage(EventId = 3011, Level = LogLevel.Information, Message = "{User} changed tag {Name}, change {ChangeSeq}")]
    public static partial void TagUpdated(ILogger logger, string user, string name, long changeSeq);

    [LoggerMessage(EventId = 3012, Level = LogLevel.Information,
        Message = "{User} deleted tag {Name}, removed from {Profiles} profile(s), change {ChangeSeq}")]
    public static partial void TagDeleted(ILogger logger, string user, string name, int profiles, long changeSeq);
}
