namespace OpenVpnPilot.Server.Data.Entities;

public sealed class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }

    // Every token descended from one sign in shares a family, so a reused token revokes all of them.
    public Guid FamilyId { get; set; }

    // Only the SHA-256 of the token is kept; the token itself exists on the client alone.
    public byte[] TokenHash { get; set; } = [];

    public Guid ClientId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    // When the person last proved who they are to the identity provider, carried through rotation.
    public DateTimeOffset AuthenticatedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public Guid? ReplacedById { get; set; }
}
