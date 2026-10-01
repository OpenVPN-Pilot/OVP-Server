using Microsoft.EntityFrameworkCore;
using OpenVpnPilot.Server.Data;
using OpenVpnPilot.Server.Data.Entities;
using OpenVpnPilot.Server.Repositories.Interfaces;

namespace OpenVpnPilot.Server.Repositories;

public sealed class UserRepository(PilotServerDbContext db) : IUserRepository
{
    public Task<User?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    // The column is citext, so this matches regardless of case.
    public Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken) =>
        db.Users.FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

    public Task<User?> FindByExternalIdAsync(AuthProviderKind provider, string externalId, CancellationToken cancellationToken) =>
        db.Users.FirstOrDefaultAsync(u => u.Provider == provider && u.ExternalId == externalId, cancellationToken);

    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking().OrderBy(u => u.Username).ToListAsync(cancellationToken);

    public void Add(User user) => db.Users.Add(user);

    public void Remove(User user) => db.Users.Remove(user);

    public Task TouchAsync(Guid id, DateTimeOffset at, CancellationToken cancellationToken) =>
        db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(s => s.SetProperty(u => u.LastSeenAt, at), cancellationToken);
}

public sealed class RefreshTokenRepository(PilotServerDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByHashAsync(byte[] hash, CancellationToken cancellationToken) =>
        db.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

    public void Add(RefreshToken token) => db.RefreshTokens.Add(token);

    public void Discard(RefreshToken token) => db.Entry(token).State = EntityState.Detached;

    // One statement, so of two refreshes racing with the same token exactly one finds it unclaimed: the
    // second waits for the first one's row lock and then sees the token already replaced.
    public async Task<bool> TryClaimAsync(Guid id, Guid replacementId, CancellationToken cancellationToken) =>
        await db.RefreshTokens
            .Where(t => t.Id == id && t.ReplacedById == null && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ReplacedById, replacementId), cancellationToken) == 1;

    public Task<int> RevokeFamilyAsync(Guid familyId, DateTimeOffset at, CancellationToken cancellationToken) =>
        db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, at), cancellationToken);

    public Task<int> RevokeAllForUserAsync(Guid userId, DateTimeOffset at, CancellationToken cancellationToken) =>
        db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, at), cancellationToken);

    public Task<int> DeleteExpiredAsync(DateTimeOffset before, CancellationToken cancellationToken) =>
        db.RefreshTokens.Where(t => t.ExpiresAt < before).ExecuteDeleteAsync(cancellationToken);
}
