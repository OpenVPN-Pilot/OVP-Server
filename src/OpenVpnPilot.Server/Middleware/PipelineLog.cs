namespace OpenVpnPilot.Server.Middleware;

internal static partial class PipelineLog
{
    [LoggerMessage(EventId = 5000, Level = LogLevel.Warning, Message = "Refused {Method} {Path} with {Status} {Code}: {Detail}")]
    public static partial void Refused(ILogger logger, string method, PathString path, int status, string code, string detail);

    [LoggerMessage(EventId = 5001, Level = LogLevel.Information, Message = "The client abandoned {Method} {Path} before it was answered")]
    public static partial void Aborted(ILogger logger, string method, PathString path);

    [LoggerMessage(EventId = 5002, Level = LogLevel.Critical,
        Message = "Stored data could not be decrypted. OVP_DATA_KEY is not the key the data was written with, or the data was altered")]
    public static partial void DecryptionFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 5003, Level = LogLevel.Error, Message = "Unhandled failure in {Method} {Path}")]
    public static partial void Unhandled(ILogger logger, string method, PathString path, Exception exception);

    [LoggerMessage(EventId = 5004, Level = LogLevel.Warning, Message = "Rate limit reached for {Method} {Path} from {Address}")]
    public static partial void RateLimited(ILogger logger, string method, PathString path, System.Net.IPAddress? address);
}
