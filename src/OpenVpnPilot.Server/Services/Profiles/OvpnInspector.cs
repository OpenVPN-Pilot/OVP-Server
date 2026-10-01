using System.Globalization;
using OpenVpnPilot.Server.Contracts;

namespace OpenVpnPilot.Server.Services.Profiles;

public sealed record OvpnFacts(
    string? RemoteHost,
    int? RemotePort,
    string? Protocol,
    bool RequiresCredentials,
    bool HasUnsupportedOptions);

// The configuration as it is stored, which can differ from the one sent in one way only: see Inspect.
public sealed record OvpnInspection(string Configuration, OvpnFacts Facts);

// A deliberately small reading of an OpenVPN configuration: enough to refuse what no client could use
// and to fill the fields a list shows. It reads lines, quotes, blocks and directives exactly as the
// client's OvpnConfigParser does, so both derive the same facts from the same text.
public static class OvpnInspector
{
    public const int MaximumLength = 256 * 1024;

    // The longest host name DNS allows, and the column the remote host is stored in.
    public const int MaximumHostLength = 255;

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

    // Checks a configuration and returns it as it is to be stored. The one change made: "auth-user-pass
    // <file>" becomes a bare "auth-user-pass". The file holds a user name and password, belongs in the
    // vault rather than here, and exists on no other machine; the bare directive makes OpenVPN ask, and
    // the client answers from the vault. Everything else is kept byte for byte.
    public static OvpnInspection Inspect(string configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration) || configuration.Length > MaximumLength)
        {
            throw Refuse(ErrorCodes.ProfileInvalidConfiguration, $"A configuration must be between 1 and {MaximumLength} characters.");
        }

        List<List<string>> directives = [];
        HashSet<string> blocks = new(StringComparer.Ordinal);
        List<OvpnLine> credentialFiles = [];
        string? openBlock = null;

        foreach (OvpnLine raw in OvpnLine.Split(configuration))
        {
            string line = raw.Text.Trim();
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

            if (IsOpeningTag(line))
            {
                openBlock = line[1..^1];
                continue;
            }

            List<string> tokens = OvpnLine.Tokenize(line);
            if (tokens is ["auth-user-pass", _, ..])
            {
                credentialFiles.Add(raw);
                tokens = ["auth-user-pass"];
            }

            directives.Add(tokens);
        }

        if (openBlock is not null)
        {
            throw Refuse(ErrorCodes.ProfileInvalidConfiguration, $"The block <{openBlock}> is never closed.");
        }

        OvpnFacts facts = Evaluate(directives, blocks);
        return new OvpnInspection(OvpnLine.Replace(configuration, credentialFiles, "auth-user-pass"), facts);
    }

    private static OvpnFacts Evaluate(List<List<string>> directives, HashSet<string> blocks)
    {
        foreach (List<string> directive in directives)
        {
            bool namesFile = FileDirectives.Contains(directive[0]) && directive.Count > 1 && !blocks.Contains(directive[0])
                && !(directive is ["dh", "none"]);
            if (namesFile)
            {
                throw Refuse(ErrorCodes.ProfileNotSelfContained,
                    $"'{directive[0]}' refers to the file '{directive[1]}'. Import the profile in the client first, which puts files inline.");
            }
        }

        if (blocks.Contains("auth-user-pass"))
        {
            throw Refuse(ErrorCodes.ProfileInvalidConfiguration,
                "The configuration carries a user name and password in an <auth-user-pass> block. Credentials belong in the vault.");
        }

        bool verifiesServer = blocks.Contains("ca") || blocks.Contains("pkcs12")
            || directives.Any(d => d[0] is "ca" or "capath" or "peer-fingerprint" or "pkcs12");
        if (!verifiesServer)
        {
            throw Refuse(ErrorCodes.ProfileNoServerVerification,
                "The configuration has no ca, capath, pkcs12 or peer-fingerprint, so OpenVPN would refuse it before connecting.");
        }

        // The first remote is the one a list shows; bare proto, rport and port count from their first line.
        List<string>? remote = directives.FirstOrDefault(d => d[0] == "remote" && d.Count > 1);
        if (remote is not null && remote[1].Length > MaximumHostLength)
        {
            throw Refuse(ErrorCodes.ProfileInvalidConfiguration, $"The remote host is longer than {MaximumHostLength} characters.");
        }

        int port = ParsePort(remote?.Count > 2 ? remote[2] : null)
            ?? ParsePort(First(directives, "rport")) ?? ParsePort(First(directives, "port")) ?? 1194;
        string protocol = (remote?.Count > 3 ? remote[3] : First(directives, "proto")) is { } p
            && p.StartsWith("tcp", StringComparison.OrdinalIgnoreCase) ? "tcp" : "udp";

        return new OvpnFacts(
            remote?[1],
            remote is null ? null : port,
            remote is null ? null : protocol,
            directives.Any(d => d[0] == "auth-user-pass"),
            directives.Any(d => ScriptDirectives.Contains(d[0])));
    }

    private static bool IsOpeningTag(string line) =>
        line.Length >= 3 && line[0] == '<' && line[1] != '/' && line[^1] == '>' && !line[1..^1].Any(char.IsWhiteSpace);

    private static string? First(List<List<string>> directives, string name) =>
        directives.FirstOrDefault(d => d[0] == name && d.Count > 1)?[1];

    private static int? ParsePort(string? text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int port) && port is > 0 and <= 65535 ? port : null;

    private static ServiceException Refuse(string code, string detail) =>
        ServiceException.BadRequest(code, detail);
}
