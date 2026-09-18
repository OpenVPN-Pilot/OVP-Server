namespace OpenVpnPilot.Server.Data.Entities;

public sealed class Profile
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? RemoteHost { get; set; }

    public int? RemotePort { get; set; }

    public string? Protocol { get; set; }

    public bool RequiresCredentials { get; set; }

    public bool HasUnsupportedOptions { get; set; }

    // Null follows the client's own setting, as it does on the client.
    public bool? ProtectRoutes { get; set; }

    public string? Notes { get; set; }

    public string? Colour { get; set; }

    // SHA-256 of the configuration, the same way the client computes it, so duplicates are found on both sides.
    public string ContentHash { get; set; } = string.Empty;

    public long ChangeSeq { get; set; }

    public uint Version { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }

    public string UpdatedBy { get; set; } = string.Empty;

    public ProfileContent? Content { get; set; }

    public List<Tag> Tags { get; set; } = [];

    public List<VaultEntry> VaultEntries { get; set; } = [];
}
