using System.Globalization;
using OpenVpnPilot.Server.Configuration;
using Serilog;
using Serilog.Events;

namespace OpenVpnPilot.Server.Logging;

public static class LoggingSetup
{
    private const string ConsoleTemplate =
        "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {RequestId} {UserName} {SourceContext}: {Message:lj}{NewLine}{Exception}";

    private const string FileTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] request={RequestId} user={UserName} "
        + "client={ClientId} version={ClientVersion} {SourceContext}: {Message:lj}{NewLine}{Exception}";

    public static void Configure(LoggerConfiguration configuration, LoggingOptions options)
    {
        configuration.MinimumLevel.Is(options.Level).Enrich.FromLogContext();

        // At the default level the framework's own request chatter and every SQL statement would bury
        // what the server itself reports. Asking for Debug is asking for all of it.
        if (options.Level >= LogEventLevel.Information)
        {
            configuration
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning);
        }

        configuration.MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information);
        configuration.WriteTo.Console(outputTemplate: ConsoleTemplate, formatProvider: CultureInfo.InvariantCulture);

        // One folder per day and one file per hour. Two open files cover the moment an hour turns over,
        // and every older file is closed so the retention job can delete its folder.
        configuration.WriteTo.Map<LogFileKey>(
            HourKey,
            (key, sink) => sink.File(
                Path.Combine(options.Directory, key.Day, key.Hour + ".log"),
                outputTemplate: FileTemplate,
                formatProvider: CultureInfo.InvariantCulture),
            sinkMapCountLimit: 2);
    }

    private static LogFileKey HourKey(LogEvent logEvent) =>
        new(
            logEvent.Timestamp.ToString(LogRetentionService.DayFormat, CultureInfo.InvariantCulture),
            logEvent.Timestamp.ToString("HH", CultureInfo.InvariantCulture));

    private readonly record struct LogFileKey(string Day, string Hour);
}
