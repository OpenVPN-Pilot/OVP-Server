using OpenVpnPilot.Server.Auth;
using OpenVpnPilot.Server.Contracts;
using OpenVpnPilot.Server.Data.Entities;
using OpenVpnPilot.Server.Repositories.Interfaces;
using OpenVpnPilot.Server.Services.Users;

namespace OpenVpnPilot.Server.Services.Auth;

public interface IUserAccountService
{
    // Finds or creates the user an identity provider vouched for, and refuses one that is switched off.
    public Task<User> ResolveAsync(ExternalIdentity identity, AuthProviderKind provider, CancellationToken cancellationToken);

    // Applies what the provider now says about an existing user, and refuses one that is gone.
    public Task ApplyAsync(User user, ProviderAccountStatus status, CancellationToken cancellationToken);
}

public sealed class UserAccountService(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IUserStateService states,
    TimeProvider time,
    ILogger<UserAccountService> logger) : IUserAccountService
{
    public async Task<User> ResolveAsync(ExternalIdentity identity, AuthProviderKind provider, CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();
        User? user = await FindExistingAsync(identity, provider, cancellationToken);
        if (user is null && identity.Disabled)
        {
            // Nothing was ever handed to this account, so there is nothing to erase and nothing to record.
            throw ServiceException.Forbidden(ErrorCodes.Forbidden, "This account is not allowed to use this server.");
        }

        if (user is null)
        {
            user = new User
            {
                Id = Guid.CreateVersion7(),
                Username = identity.Username,
                Role = identity.Role,
                State = UserState.Active,
                SecurityStamp = Guid.NewGuid(),
                CreatedAt = now,
            };
            users.Add(user);
            AuthLog.UserCreated(logger, identity.Username, provider);
        }

        user.Provider = provider;
        user.DisplayName = identity.DisplayName ?? user.DisplayName;
        user.ExternalId = identity.ExternalId ?? user.ExternalId;
        user.LastLoginAt = now;
        user.LastSeenAt = now;
        SetRole(user, identity.Role);

        ProviderAccountStatus status = identity.Disabled ? ProviderAccountStatus.Disabled : ProviderAccountStatus.Active(identity.Role);
        await ApplyAsync(user, status, cancellationToken);
        return user;
    }

    // Entra's object id never changes and is never reused, while the user principal name can be renamed
    // and later given to someone else. The id therefore decides who a record belongs to, and a known
    // name arriving with a different id is refused rather than handed another person's record. A
    // directory's distinguished name changes whenever an account moves, so there the name decides.
    private async Task<User?> FindExistingAsync(ExternalIdentity identity, AuthProviderKind provider, CancellationToken cancellationToken)
    {
        User? byName = await users.FindByUsernameAsync(identity.Username, cancellationToken);
        if (provider != AuthProviderKind.Entra || identity.ExternalId is null)
        {
            return byName;
        }

        User? byId = await users.FindByExternalIdAsync(provider, identity.ExternalId, cancellationToken);
        if (byId is null && byName is { Provider: AuthProviderKind.Entra, ExternalId: not null } && byName.ExternalId != identity.ExternalId)
        {
            AuthLog.IdentityConflict(logger, identity.Username, byName.ExternalId, identity.ExternalId);
            throw ServiceException.Conflict(ErrorCodes.IdentityConflict,
                "This name belongs to another account on this server. An administrator has to remove that account first.");
        }

        if (byId is not null && byName is not null && byName.Id != byId.Id)
        {
            AuthLog.IdentityConflict(logger, identity.Username, byName.ExternalId, identity.ExternalId);
            throw ServiceException.Conflict(ErrorCodes.IdentityConflict,
                "This account was renamed to a name another account on this server holds. An administrator has to remove that account first.");
        }

        if (byId is not null && !string.Equals(byId.Username, identity.Username, StringComparison.OrdinalIgnoreCase))
        {
            AuthLog.UserRenamed(logger, byId.Username, identity.Username);
            byId.Username = identity.Username;
        }

        return byId ?? byName;
    }

    public async Task ApplyAsync(User user, ProviderAccountStatus status, CancellationToken cancellationToken)
    {
        switch (status.Kind)
        {
            case AccountStatusKind.Missing:
                SetState(user, UserState.Deleted);
                break;
            case AccountStatusKind.Disabled:
                SetState(user, UserState.Disabled);
                break;
            case AccountStatusKind.Active when user.State != UserState.Active && user.StateSource == StateSource.Provider:
                // The provider switched it off and has switched it back on; only an administrator's decision sticks.
                SetState(user, UserState.Active);
                break;
        }

        if (status.Role is not null)
        {
            SetRole(user, status.Role.Value);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        states.Invalidate(user.Id);

        if (user.State != UserState.Active)
        {
            AuthLog.RevokedUserRefused(logger, user.Username, user.State, user.StateSource);
            throw ServiceException.Revoked("This account has been disabled or removed. Everything this server provided must be erased.");
        }
    }

    private void SetState(User user, UserState state)
    {
        if (user.State == state || (user.State != UserState.Active && user.StateSource == StateSource.Administrator && state != UserState.Active))
        {
            return;
        }

        AuthLog.ProviderChangedState(logger, user.Username, user.State, state);
        user.State = state;
        user.StateSource = state == UserState.Active ? null : StateSource.Provider;
        user.StateChangedAt = time.GetUtcNow();
        user.SecurityStamp = Guid.NewGuid();
    }

    private void SetRole(User user, UserRole role)
    {
        if (user.Role == role)
        {
            return;
        }

        // Tokens carry the role, so the old ones must stop working rather than keep the old role for minutes.
        AuthLog.RoleChanged(logger, user.Username, user.Role, role);
        user.Role = role;
        user.SecurityStamp = Guid.NewGuid();
    }
}
