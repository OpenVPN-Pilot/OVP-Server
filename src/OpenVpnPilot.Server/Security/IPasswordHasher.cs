namespace OpenVpnPilot.Server.Security;

public interface IPasswordHasher
{
    public bool IsHash(string stored);

    public string Hash(string password);

    public bool Verify(string password, string stored);
}
