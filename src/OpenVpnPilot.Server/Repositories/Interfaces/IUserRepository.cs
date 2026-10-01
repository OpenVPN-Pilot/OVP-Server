using OpenVpnPilot.Server.Data.Entities;

namespace OpenVpnPilot.Server.Repositories.Interfaces;

public interface IUserRepository
{
    public Task<User?> FindAsync(Guid id, CancellationToken cancellationToken);

    public Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken);

    public Task<User?> FindByExternalIdAsync(AuthProviderKind provider, string externalId, CancellationToken cancellationToken);

    public Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken);

    public void Add(User user);

    public void Remove(User user);

    public Task TouchAsync(Guid id, DateTimeOffset at, CancellationToken cancellationToken);
}

public interface IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByHashAsync(byte[] hash, CancellationToken cancellationToken);

    public void Add(RefreshToken token);

    // Forgets a token that was added but must not be stored after all.
    public void Discard(RefreshToken token);

    // Marks the token as replaced unless it already was; false means another refresh got there first.
    public Task<bool> TryClaimAsync(Guid id, Guid replacementId, CancellationToken cancellationToken);

    public Task<int> RevokeFamilyAsync(Guid familyId, DateTimeOffset at, CancellationToken cancellationToken);

    public Task<int> RevokeAllForUserAsync(Guid userId, DateTimeOffset at, CancellationToken cancellationToken);

    public Task<int> DeleteExpiredAsync(DateTimeOffset before, CancellationToken cancellationToken);
}
