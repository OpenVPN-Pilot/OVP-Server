using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using OpenVpnPilot.Server.Configuration;

namespace OpenVpnPilot.Server.Security;

public static class CertificateLoader
{
    // Accepts a PKCS#12 bundle, or a PEM certificate with its key in a second file, optionally encrypted.
    public static X509Certificate2 Load(TlsOptions options)
    {
        string path = options.CertificatePath!;
        try
        {
            if (path.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".p12", StringComparison.OrdinalIgnoreCase))
            {
                return X509CertificateLoader.LoadPkcs12FromFile(path, options.CertificatePassword);
            }

            if (options.KeyPath is null)
            {
                throw new ConfigurationException(["OVP_TLS_KEY_PATH is required when OVP_TLS_CERT_PATH is a PEM certificate."]);
            }

            using X509Certificate2 pem = options.CertificatePassword is null
                ? X509Certificate2.CreateFromPemFile(path, options.KeyPath)
                : X509Certificate2.CreateFromEncryptedPemFile(path, options.CertificatePassword, options.KeyPath);

            // A key loaded from PEM is ephemeral, which TLS on some platforms refuses. A round trip through
            // PKCS#12 gives it a form every platform accepts.
            return X509CertificateLoader.LoadPkcs12(pem.Export(X509ContentType.Pkcs12), null);
        }
        catch (Exception exception) when (exception is IOException or CryptographicException or UnauthorizedAccessException)
        {
            throw new ConfigurationException([$"OVP_TLS_CERT_PATH '{path}' could not be loaded: {exception.Message}"]);
        }
    }
}
