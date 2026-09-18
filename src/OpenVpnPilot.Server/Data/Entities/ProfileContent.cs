namespace OpenVpnPilot.Server.Data.Entities;

// Kept apart from the profile so a list of profiles never loads the configurations with it.
public sealed class ProfileContent
{
    public Guid ProfileId { get; set; }

    // The whole configuration including its private keys, encrypted with the data key.
    public byte[] Cipher { get; set; } = [];
}
