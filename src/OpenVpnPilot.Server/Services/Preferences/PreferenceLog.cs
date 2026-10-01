namespace OpenVpnPilot.Server.Services.Preferences;

internal static partial class PreferenceLog
{
    [LoggerMessage(EventId = 3300, Level = LogLevel.Information, Message = "{User} stored {Count} favourite(s)")]
    public static partial void FavouritesStored(ILogger logger, string user, int count);

    [LoggerMessage(EventId = 3301, Level = LogLevel.Information, Message = "{User} stored {Count} shortcut(s)")]
    public static partial void HotkeysStored(ILogger logger, string user, int count);

    [LoggerMessage(EventId = 3302, Level = LogLevel.Information, Message = "{User} stored settings of schema {SchemaVersion}, {Bytes} bytes")]
    public static partial void SettingsStored(ILogger logger, string user, int schemaVersion, int bytes);
}
