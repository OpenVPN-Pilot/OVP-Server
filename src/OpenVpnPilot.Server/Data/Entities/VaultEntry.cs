namespace OpenVpnPilot.Server.Data.Entities;

// One shared sign in per profile and realm. The realm is OpenVPN's: Auth for a user name and password,
// or the name of a private key for its passphrase.
public sealed class VaultEntry
{
    public Guid ProfileId { get; set; }

    public Profile? Profile { get; set; }

    public string Realm { get; set; } = string.Empty;

    // User name and password together, encrypted with the data key.
    public byte[] Cipher { get; set; } = [];

    public long ChangeSeq { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }

    public string UpdatedBy { get; set; } = string.Empty;
}
