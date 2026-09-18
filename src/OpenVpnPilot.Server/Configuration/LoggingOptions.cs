using Serilog.Events;

namespace OpenVpnPilot.Server.Configuration;

public sealed record LoggingOptions
{
    public required LogEventLevel Level { get; init; }

    public required string Directory { get; init; }

    public required int RetentionDays { get; init; }

    public static LoggingOptions Read(EnvironmentReader env) => new()
    {
        Level = env.Choice("OVP_LOG_LEVEL", (LogEventLevel?)LogEventLevel.Information),
        Directory = env.Text("OVP_LOG_DIRECTORY", "/app/logs"),
        RetentionDays = env.WholeNumber("OVP_LOG_RETENTION_DAYS", 7, 1, 3650),
    };
}
