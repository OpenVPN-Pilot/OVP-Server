namespace OpenVpnPilot.Server.Security;

public interface ISecretCipher
{
    // The context is authenticated with the data, so a value moved to another row does not decrypt.
    public byte[] Encrypt(string plaintext, string context);

    public string Decrypt(byte[] cipher, string context);
}
