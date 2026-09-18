using System.Security.Cryptography;
using System.Text;
using OpenVpnPilot.Server.Configuration;

namespace OpenVpnPilot.Server.Security;

// Layout: one format byte, the nonce, the tag, the ciphertext. The format byte leaves room for a
// second key or algorithm without guessing what an existing value is.
public sealed class AesGcmSecretCipher(SecurityOptions options) : ISecretCipher
{
    private const byte FormatVersion = 1;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int HeaderSize = 1 + NonceSize + TagSize;

    public byte[] Encrypt(string plaintext, string context)
    {
        byte[] plain = Encoding.UTF8.GetBytes(plaintext);
        byte[] result = new byte[HeaderSize + plain.Length];
        result[0] = FormatVersion;

        Span<byte> nonce = result.AsSpan(1, NonceSize);
        Span<byte> tag = result.AsSpan(1 + NonceSize, TagSize);
        Span<byte> cipher = result.AsSpan(HeaderSize);
        RandomNumberGenerator.Fill(nonce);

        using AesGcm aes = new(options.DataKey, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag, Encoding.UTF8.GetBytes(context));
        CryptographicOperations.ZeroMemory(plain);
        return result;
    }

    public string Decrypt(byte[] cipher, string context)
    {
        if (cipher.Length < HeaderSize || cipher[0] != FormatVersion)
        {
            throw new CryptographicException($"Stored value for {context} is not in a known format.");
        }

        byte[] plain = new byte[cipher.Length - HeaderSize];
        using AesGcm aes = new(options.DataKey, TagSize);

        // A failure here almost always means OVP_DATA_KEY differs from the key the value was written with.
        aes.Decrypt(
            cipher.AsSpan(1, NonceSize),
            cipher.AsSpan(HeaderSize),
            cipher.AsSpan(1 + NonceSize, TagSize),
            plain,
            Encoding.UTF8.GetBytes(context));

        string text = Encoding.UTF8.GetString(plain);
        CryptographicOperations.ZeroMemory(plain);
        return text;
    }
}
