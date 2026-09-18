using System.Globalization;
using OpenVpnPilot.Server.Contracts;

namespace OpenVpnPilot.Server.Services.Profiles;

public sealed record OvpnFacts(
    string? RemoteHost,
    int? RemotePort,
    string? Protocol,
    bool RequiresCredentials,
    bool HasUnsupportedOptions);

// A deliberately small reading of an OpenVPN configuration: enough to refuse what no client could use
// and to fill the fields a list shows. The client's own parser remains the authority on everything else.
public static class OvpnInspector
{
    public const int MaximumLength = 256 * 1024;

    // Directives that name a file. On the server there is no file, so they must arrive as inline blocks.
    private static readonly HashSet<string> FileDirectives = new(StringComparer.Ordinal)
    {
        "ca", "cert", "key", "dh", "extra-certs", "pkcs12", "crl-verify", "secret", "tls-auth", "tls-crypt", "tls-crypt-v2",
    };

    // The same list the client warns about: options that make OpenVPN run a program.
    private static readonly HashSet<string> ScriptDirectives = new(StringComparer.Ordinal)
    {
        "up", "down", "route-up", "route-pre-down", "ipchange", "tls-verify", "auth-user-pass-verify",
        "client-connect", "client-disconnect", "learn-address", "auth-user-pass-optional",
    };

    public static OvpnFacts Inspect(string configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration) || configuration.Length > MaximumLength)
        {
            throw Refuse(ErrorCodes.ProfileInvalidConfiguration, $"A configuration must be between 1 and {MaximumLength} characters.");
        }

        List<string[]> directives = [];
        HashSet<string> blocks = new(StringComparer.Ordinal);
        string? openBlock = null;

        foreach (string raw in configuration.Split('\n'))
        {
            string line = raw.Trim();
            if (openBlock is not null)
            {
                if (line == $"</{openBlock}>")
                {
                    blocks.Add(openBlock);
                    openBlock = null;
                }

                continue;
            }

            if (line.Length == 0 || line[0] is '#' or ';')
            {
                continue;
            }

            if (line.StartsWith('<') && line.EndsWith('>') && !line.StartsWith("</", StringComparison.Ordinal))
            {
                openBlock = line[1..^1];
                continue;
            }

            string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            parts[0] = parts[0].TrimStart('-').ToLowerInvariant();
            directives.Add(parts);
        }

        if (openBlock is not null)
        {
            throw Refuse(ErrorCodes.ProfileInvalidConfiguration, $"The block <{openBlock}> is never closed.");
        }

        return Evaluate(directives, blocks);
    }

    private static OvpnFacts Evaluate(List<string[]> directives, HashSet<string> blocks)
    {
        foreach (string[] directive in directives)
        {
            if (FileDirectives.Contains(directive[0]) && directive.Length > 1 && !IsInlineMarker(directive[1]))
            {
                throw Refuse(ErrorCodes.ProfileNotSelfContained,
                    $"'{directive[0]}' refers to the file '{directive[1]}'. Import the profile in the client first, which puts files inline.");
            }

            if (directive[0] == "auth-user-pass" && directive.Length > 1)
            {
                throw Refuse(ErrorCodes.ProfileNotSelfContained,
                    "'auth-user-pass' names a credentials file. Credentials belong in the vault, not in the configuration.");
            }
        }

        bool verifiesServer = blocks.Contains("ca") || blocks.Contains("pkcs12")
            || directives.Any(d => d[0] is "capath" or "peer-fingerprint") || blocks.Contains("peer-fingerprint");
        if (!verifiesServer)
        {
            throw Refuse(ErrorCodes.ProfileNoServerVerification,
                "The configuration has no ca, capath, pkcs12 or peer-fingerprint, so OpenVPN would refuse it before connecting.");
        }

        string[]? remote = directives.FirstOrDefault(d => d[0] == "remote" && d.Length > 1);
        string? protocol = Normalise(remote?.Length > 3 ? remote[3] : Value(directives, "proto")) ?? "udp";
        int port = ParsePort(remote?.Length > 2 ? remote[2] : null)
            ?? ParsePort(Value(directives, "rport")) ?? ParsePort(Value(directives, "port")) ?? 1194;

        return new OvpnFacts(
            remote?[1],
            remote is null ? null : port,
            remote is null ? null : protocol,
            directives.Any(d => d[0] == "auth-user-pass"),
            directives.Any(d => ScriptDirectives.Contains(d[0])));
    }

    private static bool IsInlineMarker(string argument) => argument == "[inline]";

    private static string? Value(List<string[]> directives, string name) =>
        directives.LastOrDefault(d => d[0] == name && d.Length > 1)?[1];

    private static string? Normalise(string? protocol) => protocol?.ToLowerInvariant() switch
    {
        null => null,
        var p when p.StartsWith("tcp", StringComparison.Ordinal) => "tcp",
        var p when p.StartsWith("udp", StringComparison.Ordinal) => "udp",
        _ => null,
    };

    private static int? ParsePort(string? text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int port) && port is > 0 and <= 65535 ? port : null;

    private static ServiceException Refuse(string code, string detail) =>
        ServiceException.BadRequest(code, detail);
}
