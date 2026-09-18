namespace OpenVpnPilot.Server.Data.Entities;

public sealed class User
{
    public Guid Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public AuthProviderKind Provider { get; set; }

    // The directory's distinguished name or the Entra object id. Names can be reused; these cannot.
    public string? ExternalId { get; set; }

    public UserRole Role { get; set; }

    public UserState State { get; set; }

    public StateSource? StateSource { get; set; }

    public DateTimeOffset? StateChangedAt { get; set; }

    // Changes whenever every token the user holds must stop working.
    public Guid SecurityStamp { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    public DateTimeOffset? LastSeenAt { get; set; }

    public List<RefreshToken> RefreshTokens { get; set; } = [];
}
