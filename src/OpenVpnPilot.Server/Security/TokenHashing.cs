using System.Security.Cryptography;
using System.Text;

namespace OpenVpnPilot.Server.Security;

public static class TokenHashing
{
    public static string NewToken() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    // A refresh token is random and long, so a plain hash is enough to make a stolen table useless.
    public static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    public static string ContentHash(string configuration) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(configuration)));

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
