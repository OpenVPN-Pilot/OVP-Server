using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Novell.Directory.Ldap;
using OpenVpnPilot.Server.Configuration;

namespace OpenVpnPilot.Server.Auth.Ldap;

// Opens encrypted connections only. A directory reached in the clear would carry every password
// typed into a client across the network as it is.
public sealed class LdapConnector
{
    private readonly LdapOptions options;
    private readonly X509Certificate2? authority;

    public LdapConnector(AuthOptions auth)
    {
        options = auth.Ldap ?? throw new InvalidOperationException("LDAP options are missing.");
        authority = options.CaCertificatePath is null
            ? null
            : X509CertificateLoader.LoadCertificateFromFile(options.CaCertificatePath);
    }

    public async Task<LdapConnection> OpenAsync()
    {
        LdapConnectionOptions connectionOptions = new LdapConnectionOptions()
            .ConfigureRemoteCertificateValidationCallback(ValidateCertificate);
        if (options.Security == LdapSecurity.Ldaps)
        {
            connectionOptions = connectionOptions.UseSsl();
        }

        LdapConnection connection = new(connectionOptions) { ConnectionTimeout = 10_000 };
        try
        {
            await connection.ConnectAsync(options.Host, options.Port);
            if (options.Security == LdapSecurity.StartTls)
            {
                await connection.StartTlsAsync();
            }

            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    public async Task<LdapConnection> OpenAsServiceAsync()
    {
        LdapConnection connection = await OpenAsync();
        try
        {
            await connection.BindAsync(options.BindDn, options.BindPassword);
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private bool ValidateCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        if (errors == SslPolicyErrors.None)
        {
            return true;
        }

        // Only the chain may be fixed by a named authority. A certificate for another host stays refused.
        if (authority is null || certificate is null || errors != SslPolicyErrors.RemoteCertificateChainErrors)
        {
            return false;
        }

        using X509Chain custom = new();
        custom.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        custom.ChainPolicy.CustomTrustStore.Add(authority);
        custom.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        using X509Certificate2 presented = X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
        return custom.Build(presented);
    }
}
