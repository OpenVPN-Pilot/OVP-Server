using System.Text.Json;
using OpenVpnPilot.Server.Contracts.Responses;
using OpenVpnPilot.Server.Data.Entities;
using OpenVpnPilot.Server.Security;

namespace OpenVpnPilot.Server.Services.Vault;

// User name and password are encrypted together, bound to the profile and realm they belong to.
public sealed class VaultCodec(ISecretCipher cipher)
{
    public void Write(VaultEntry entry, string? username, string password) =>
        entry.Cipher = cipher.Encrypt(JsonSerializer.Serialize(new Secret(username, password)), Context(entry));

    public VaultEntryResponse Read(VaultEntry entry)
    {
        Secret secret = JsonSerializer.Deserialize<Secret>(cipher.Decrypt(entry.Cipher, Context(entry)))
            ?? throw new InvalidDataException($"The vault entry {Context(entry)} decrypted to nothing.");
        return new VaultEntryResponse(
            entry.ProfileId,
            entry.Realm,
            secret.U,
            secret.P,
            entry.ChangeSeq,
            entry.CreatedAt,
            entry.CreatedBy,
            entry.UpdatedAt,
            entry.UpdatedBy);
    }

    private static string Context(VaultEntry entry) => $"vault:{entry.ProfileId:N}:{entry.Realm}";

    private sealed record Secret(string? U, string P);
}
