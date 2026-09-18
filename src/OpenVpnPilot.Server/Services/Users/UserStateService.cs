using Microsoft.Extensions.Caching.Memory;
using OpenVpnPilot.Server.Data.Entities;
using OpenVpnPilot.Server.Repositories.Interfaces;

namespace OpenVpnPilot.Server.Services.Users;

public sealed record UserSnapshot(Guid Id, string Username, UserRole Role, UserState State, Guid SecurityStamp, AuthProviderKind Provider);

public interface IUserStateService
{
    // What every authenticated request is checked against. Null means the user no longer exists at all.
    public Task<UserSnapshot?> GetAsync(Guid userId, CancellationToken cancellationToken);

    public void Invalidate(Guid userId);

    // Records the last request, at most once every few minutes per user rather than on every call.
    public Task TouchAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed class UserStateService(IMemoryCache cache, IUserRepository users, TimeProvider time) : IUserStateService
{
    // Short enough that a change made on another server instance is noticed quickly; changes made
    // through this instance invalidate the entry at once.
    private static readonly TimeSpan SnapshotLifetime = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(5);

    public async Task<UserSnapshot?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(SnapshotKey(userId), out UserSnapshot? cached))
        {
            return cached;
        }

        User? user = await users.FindAsync(userId, cancellationToken);
        UserSnapshot? snapshot = user is null
            ? null
            : new UserSnapshot(user.Id, user.Username, user.Role, user.State, user.SecurityStamp, user.Provider);
        cache.Set(SnapshotKey(userId), snapshot, SnapshotLifetime);
        return snapshot;
    }

    public void Invalidate(Guid userId) => cache.Remove(SnapshotKey(userId));

    public async Task TouchAsync(Guid userId, CancellationToken cancellationToken)
    {
        string key = "touch:" + userId.ToString("N");
        if (cache.TryGetValue(key, out _))
        {
            return;
        }

        cache.Set(key, true, TouchInterval);
        await users.TouchAsync(userId, time.GetUtcNow(), cancellationToken);
    }

    private static string SnapshotKey(Guid userId) => "user:" + userId.ToString("N");
}
