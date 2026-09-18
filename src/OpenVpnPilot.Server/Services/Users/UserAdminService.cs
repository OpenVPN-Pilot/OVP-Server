using OpenVpnPilot.Server.Auth;
using OpenVpnPilot.Server.Contracts;
using OpenVpnPilot.Server.Contracts.Responses;
using OpenVpnPilot.Server.Data.Entities;
using OpenVpnPilot.Server.Mapping;
using OpenVpnPilot.Server.Repositories.Interfaces;

namespace OpenVpnPilot.Server.Services.Users;

public interface IUserAdminService
{
    public Task<IReadOnlyList<UserResponse>> ListAsync(CancellationToken cancellationToken);

    public Task<UserResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    // The user's clients are told to wipe themselves on their next request.
    public Task<UserResponse> DisableAsync(Guid id, CancellationToken cancellationToken);

    public Task<UserResponse> EnableAsync(Guid id, CancellationToken cancellationToken);

    // Without purge the row stays as a record that the account was removed, which keeps the wipe working
    // and keeps the name from simply signing in again.
    public Task DeleteAsync(Guid id, bool purge, CancellationToken cancellationToken);

    // Signs the user out everywhere without erasing anything; they may sign in again at once.
    public Task<UserResponse> RevokeTokensAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class UserAdminService(
    IUserRepository users,
    IRefreshTokenRepository tokens,
    IUnitOfWork unitOfWork,
    IUserStateService states,
    ICurrentUser currentUser,
    TimeProvider time,
    ILogger<UserAdminService> logger) : IUserAdminService
{
    public async Task<IReadOnlyList<UserResponse>> ListAsync(CancellationToken cancellationToken) =>
        [.. (await users.ListAsync(cancellationToken)).Select(u => u.ToResponse())];

    public async Task<UserResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        (await FindAsync(id, cancellationToken)).ToResponse();

    public Task<UserResponse> DisableAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeStateAsync(id, UserState.Disabled, cancellationToken);

    public Task<UserResponse> EnableAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeStateAsync(id, UserState.Active, cancellationToken);

    public async Task DeleteAsync(Guid id, bool purge, CancellationToken cancellationToken)
    {
        if (!purge)
        {
            await ChangeStateAsync(id, UserState.Deleted, cancellationToken);
            return;
        }

        User user = await FindOtherAsync(id, cancellationToken);
        users.Remove(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        states.Invalidate(id);
        UserAdminLog.Purged(logger, currentUser.Username, user.Username);
    }

    public async Task<UserResponse> RevokeTokensAsync(Guid id, CancellationToken cancellationToken)
    {
        User user = await FindAsync(id, cancellationToken);
        user.SecurityStamp = Guid.NewGuid();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        int revoked = await tokens.RevokeAllForUserAsync(id, time.GetUtcNow(), cancellationToken);
        states.Invalidate(id);
        UserAdminLog.TokensRevoked(logger, currentUser.Username, user.Username, revoked);
        return user.ToResponse();
    }

    private async Task<UserResponse> ChangeStateAsync(Guid id, UserState state, CancellationToken cancellationToken)
    {
        User user = await FindOtherAsync(id, cancellationToken);
        UserState previous = user.State;
        user.State = state;
        user.StateSource = state == UserState.Active ? null : StateSource.Administrator;
        user.StateChangedAt = time.GetUtcNow();
        user.SecurityStamp = Guid.NewGuid();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (state != UserState.Active)
        {
            await tokens.RevokeAllForUserAsync(id, time.GetUtcNow(), cancellationToken);
        }

        states.Invalidate(id);
        UserAdminLog.StateChanged(logger, currentUser.Username, user.Username, previous, state);
        return user.ToResponse();
    }

    private async Task<User> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await users.FindAsync(id, cancellationToken)
            ?? throw ServiceException.NotFound(ErrorCodes.UserNotFound, $"There is no user {id}.");

    // An administrator locking themselves out would leave nobody to undo it.
    private async Task<User> FindOtherAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == currentUser.Id)
        {
            throw ServiceException.Conflict(ErrorCodes.UserSelfModification, "Administrators cannot disable or delete their own account.");
        }

        return await FindAsync(id, cancellationToken);
    }
}

internal static partial class UserAdminLog
{
    [LoggerMessage(EventId = 4000, Level = LogLevel.Warning, Message = "{Admin} changed user {Username} from {From} to {To}")]
    public static partial void StateChanged(ILogger logger, string admin, string username, UserState from, UserState to);

    [LoggerMessage(EventId = 4001, Level = LogLevel.Warning, Message = "{Admin} purged user {Username} and everything stored for them")]
    public static partial void Purged(ILogger logger, string admin, string username);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Information, Message = "{Admin} signed out user {Username} everywhere, {Count} token(s) revoked")]
    public static partial void TokensRevoked(ILogger logger, string admin, string username, int count);
}
