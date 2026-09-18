namespace OpenVpnPilot.Server.Data.Entities;

public sealed class UserFavourite
{
    public Guid UserId { get; set; }

    public Guid ProfileId { get; set; }

    // 1 to 9, bound on the client to the favourite shortcuts.
    public int? Slot { get; set; }
}

public sealed class UserHotkey
{
    public Guid UserId { get; set; }

    public string ActionId { get; set; } = string.Empty;

    public string Gesture { get; set; } = string.Empty;

    public Guid? ProfileId { get; set; }

    public bool IsEnabled { get; set; }
}

public sealed class UserSettingsDocument
{
    public Guid UserId { get; set; }

    public int SchemaVersion { get; set; }

    // The client's portable settings as it defines them. The server keeps them and does not interpret them.
    public string Document { get; set; } = "{}";

    public uint Version { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
