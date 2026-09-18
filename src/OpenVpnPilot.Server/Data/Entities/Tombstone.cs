namespace OpenVpnPilot.Server.Data.Entities;

public enum TombstoneKind
{
    Profile,
    Tag,
    VaultEntry,
}

// A record that something existed and was removed, so a client synchronising from a cursor learns of it.
public sealed class Tombstone
{
    public long Id { get; set; }

    public TombstoneKind Kind { get; set; }

    public Guid EntityId { get; set; }

    // Only set for vault entries, which are keyed by profile and realm.
    public string? Realm { get; set; }

    public long ChangeSeq { get; set; }

    public DateTimeOffset DeletedAt { get; set; }
}
