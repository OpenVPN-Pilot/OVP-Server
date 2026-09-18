using System.Globalization;
using OpenVpnPilot.Server.Configuration;

namespace OpenVpnPilot.Server.Logging;

// A busy server writes a lot, and nothing else ever removes it, so the day folders are pruned here.
public sealed class LogRetentionService(
    LoggingOptions options,
    TimeProvider time,
    ILogger<LogRetentionService> logger) : BackgroundService
{
    public const string DayFormat = "yyyy-MM-dd";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromHours(1), time);
        do
        {
            Prune();
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private void Prune()
    {
        if (!Directory.Exists(options.Directory))
        {
            return;
        }

        DateOnly today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        DateOnly oldestKept = today.AddDays(-options.RetentionDays);

        foreach (string folder in Directory.EnumerateDirectories(options.Directory))
        {
            string name = Path.GetFileName(folder);
            if (!DateOnly.TryParseExact(name, DayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly day)
                || day >= oldestKept)
            {
                continue;
            }

            try
            {
                Directory.Delete(folder, recursive: true);
                HostLog.LogFolderDeleted(logger, name, options.RetentionDays);
            }
            catch (IOException exception)
            {
                HostLog.LogFolderNotDeleted(logger, name, exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                HostLog.LogFolderNotDeleted(logger, name, exception);
            }
        }
    }
}
