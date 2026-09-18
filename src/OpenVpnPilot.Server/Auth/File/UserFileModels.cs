namespace OpenVpnPilot.Server.Auth.File;

// The shape of users.yaml. Property names map to camelCase keys: username, password, role,
// displayName, disabled.
public sealed class UserFileDocument
{
    public List<UserFileEntry> Users { get; set; } = [];
}

public sealed class UserFileEntry
{
    public string Username { get; set; } = string.Empty;

    // An Argon2id hash from 'hash-password', or a plain password, which works and is warned about.
    public string Password { get; set; } = string.Empty;

    public string Role { get; set; } = "user";

    public string? DisplayName { get; set; }

    public bool Disabled { get; set; }
}
