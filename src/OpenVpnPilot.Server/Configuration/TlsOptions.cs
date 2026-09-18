using System.Net;

namespace OpenVpnPilot.Server.Configuration;

public enum TlsMode
{
    Kestrel,
    Proxy,
}

public sealed record TlsOptions
{
    public required TlsMode Mode { get; init; }

    public required int HttpsPort { get; init; }

    public required int HttpPort { get; init; }

    public string? CertificatePath { get; init; }

    public string? KeyPath { get; init; }

    public string? CertificatePassword { get; init; }

    public required IReadOnlyList<IPNetwork> TrustedProxies { get; init; }

    public static TlsOptions Read(EnvironmentReader env)
    {
        TlsMode mode = env.Choice<TlsMode>("OVP_TLS_MODE", null);
        TlsOptions options = new()
        {
            Mode = mode,
            HttpsPort = env.WholeNumber("OVP_HTTPS_PORT", 8443, 1, 65535),
            HttpPort = env.WholeNumber("OVP_HTTP_PORT", 8080, 1, 65535),
            CertificatePath = env.Optional("OVP_TLS_CERT_PATH"),
            KeyPath = env.Optional("OVP_TLS_KEY_PATH"),
            CertificatePassword = env.Optional("OVP_TLS_CERT_PASSWORD"),
            TrustedProxies = ReadNetworks(env, "OVP_TRUSTED_PROXIES"),
        };

        if (mode == TlsMode.Kestrel && options.CertificatePath is null)
        {
            env.Fail("OVP_TLS_CERT_PATH", "is required when OVP_TLS_MODE is kestrel.");
        }

        if (mode == TlsMode.Proxy && options.TrustedProxies.Count == 0)
        {
            // Without a trusted network any caller could claim the request arrived over HTTPS.
            env.Fail("OVP_TRUSTED_PROXIES", "is required when OVP_TLS_MODE is proxy, for example 172.16.0.0/12.");
        }

        return options;
    }

    private static List<IPNetwork> ReadNetworks(EnvironmentReader env, string name)
    {
        List<IPNetwork> networks = [];
        foreach (string entry in env.List(name))
        {
            string cidr = entry.Contains('/', StringComparison.Ordinal) ? entry : AsSingleHost(entry);
            if (IPNetwork.TryParse(cidr, out IPNetwork network))
            {
                networks.Add(network);
            }
            else
            {
                env.Fail(name, $"contains '{entry}', which is not an address or a network in CIDR form.");
            }
        }

        return networks;
    }

    private static string AsSingleHost(string address) =>
        IPAddress.TryParse(address, out IPAddress? parsed)
            ? $"{address}/{(parsed.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? 128 : 32)}"
            : address;
}
