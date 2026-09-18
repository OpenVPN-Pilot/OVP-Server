namespace OpenVpnPilot.Server.Contracts;

public static class PilotHeaders
{
    public const string ClientVersion = "X-Pilot-Client-Version";
    public const string ApiVersion = "X-Pilot-Api-Version";
    public const string ClientId = "X-Pilot-Client-Id";
    public const string Platform = "X-Pilot-Platform";
    public const string Timestamp = "X-Pilot-Timestamp";
    public const string RequestId = "X-Pilot-Request-Id";

    public const string ServerVersion = "X-Pilot-Server-Version";

    // Tells a client that the account is gone and everything it received from this server must be erased.
    public const string Directive = "X-Pilot-Directive";
    public const string WipeDirective = "wipe";

    public const string CurrentApiVersion = "1";

    public static readonly IReadOnlySet<string> Platforms =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "windows", "macos", "linux" };
}
