using System.Security.Cryptography.X509Certificates;
using OpenVpnPilot.Server.Configuration;
using OpenVpnPilot.Server.Logging;
using OpenVpnPilot.Server.Security;

namespace OpenVpnPilot.Server.Extensions;

public static class HostingSetup
{
    // In kestrel mode only an HTTPS listener exists, so plain HTTP cannot even be attempted. In proxy mode
    // the server listens in the clear for the proxy, and HttpsRequirementMiddleware refuses anything the
    // proxy did not receive over HTTPS.
    public static X509Certificate2? ConfigurePilotKestrel(this WebApplicationBuilder builder, TlsOptions tls)
    {
        X509Certificate2? certificate = tls.Mode == TlsMode.Kestrel ? CertificateLoader.Load(tls) : null;
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.AddServerHeader = false;
            if (certificate is not null)
            {
                kestrel.ListenAnyIP(tls.HttpsPort, listen => listen.UseHttps(certificate));
            }
            else
            {
                kestrel.ListenAnyIP(tls.HttpPort);
            }
        });
        return certificate;
    }

    public static void ReportListeners(ILogger logger, TlsOptions tls, X509Certificate2? certificate, TimeProvider time)
    {
        if (certificate is null)
        {
            HostLog.ListeningBehindProxy(logger, tls.HttpPort, tls.TrustedProxies);
            return;
        }

        DateTime notAfter = certificate.NotAfter.ToUniversalTime();
        string subject = certificate.Subject;
        HostLog.ListeningHttps(logger, tls.HttpsPort, subject, notAfter);
        int days = (int)(notAfter - time.GetUtcNow().UtcDateTime).TotalDays;
        if (days < 30)
        {
            HostLog.CertificateExpiring(logger, notAfter, days);
        }
    }
}
