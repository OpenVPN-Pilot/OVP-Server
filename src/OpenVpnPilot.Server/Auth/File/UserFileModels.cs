using OpenVpnPilot.Server.Data.Entities;

namespace OpenVpnPilot.Server.Auth.File;

// The shape of users.yaml. Property names map to camelCase keys: username, password, role,
// displayName, disabled.
public sealed class UserFileDocument
{
    // Nullable because YAML can say "users:" with nothing after it, and does not care what C# promised.
    public List<UserFileEntry?>? Users { get; set; } = [];
}

public sealed class UserFileEntry
{
    public string? Username { get; set; } = string.Empty;

    // An Argon2id hash from 'hash-password', or a plain password, which works and is warned about.
    public string? Password { get; set; } = string.Empty;

    public string? Role { get; set; } = "user";

    public string? DisplayName { get; set; }

    public bool Disabled { get; set; }
}

// One user as the store holds it after the file was checked: every value present and the role decided.
public sealed record UserFileUser(string Username, string Password, UserRole Role, string? DisplayName, bool Disabled);
