namespace OpenVpnPilot.Server.Data.Entities;

public sealed class Tag
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Colour { get; set; }

    public long ChangeSeq { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<Profile> Profiles { get; set; } = [];
}
