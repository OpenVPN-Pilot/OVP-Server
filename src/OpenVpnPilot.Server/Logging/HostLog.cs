using OpenVpnPilot.Server.Configuration;

namespace OpenVpnPilot.Server.Logging;

// Event ids by area: 1000 host and database, 1100 maintenance, 2000 sign in, 2100 tokens and
// revocation, 3000 profiles and tags, 3100 vault, 3200 synchronisation, 3300 preferences,
// 4000 user administration, 5000 request pipeline.
internal static partial class HostLog
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information,
        Message = "OpenVPN Pilot Server {Version} starting: authentication {AuthMode}, TLS {TlsMode}, Swagger {Swagger}")]
    public static partial void Starting(ILogger logger, string version, AuthMode authMode, TlsMode tlsMode, bool swagger);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Database schema is current")]
    public static partial void SchemaCurrent(ILogger logger);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "Applying {Count} database migration(s): {Migrations}")]
    public static partial void Migrating(ILogger logger, int count, IReadOnlyList<string> migrations);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information, Message = "Database schema migrated")]
    public static partial void Migrated(ILogger logger);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Warning,
        Message = "Automatic migration is off. The schema must already match this version, or requests will fail")]
    public static partial void MigrationSkipped(ILogger logger);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Information, Message = "Listening for HTTPS on port {Port} with certificate {Subject}, valid until {NotAfter:yyyy-MM-dd HH:mm} UTC")]
    public static partial void ListeningHttps(ILogger logger, int port, string subject, DateTime notAfter);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Information,
        Message = "Listening for HTTP on port {Port} behind a reverse proxy; requests count as HTTPS only from {Proxies}")]
    public static partial void ListeningBehindProxy(ILogger logger, int port, IReadOnlyList<System.Net.IPNetwork> proxies);

    [LoggerMessage(EventId = 1007, Level = LogLevel.Warning, Message = "The TLS certificate expires on {NotAfter:yyyy-MM-dd HH:mm} UTC, in {Days} day(s)")]
    public static partial void CertificateExpiring(ILogger logger, DateTime notAfter, int days);

    [LoggerMessage(EventId = 1100, Level = LogLevel.Information, Message = "Deleted log folder {Folder}, older than {Days} days")]
    public static partial void LogFolderDeleted(ILogger logger, string folder, int days);

    [LoggerMessage(EventId = 1101, Level = LogLevel.Warning, Message = "Could not delete log folder {Folder}; it is tried again in an hour")]
    public static partial void LogFolderNotDeleted(ILogger logger, string folder, Exception exception);

    [LoggerMessage(EventId = 1102, Level = LogLevel.Information,
        Message = "Maintenance removed {Tokens} expired refresh token(s) and {Tombstones} old deletion record(s)")]
    public static partial void MaintenanceDone(ILogger logger, int tokens, int tombstones);

    [LoggerMessage(EventId = 1103, Level = LogLevel.Error, Message = "Maintenance failed; it runs again in an hour")]
    public static partial void MaintenanceFailed(ILogger logger, Exception exception);
}
