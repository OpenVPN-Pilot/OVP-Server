using OpenVpnPilot.Server.Contracts;
using OpenVpnPilot.Server.Data.Entities;
using OpenVpnPilot.Server.Security;
using OpenVpnPilot.Server.Services;

namespace OpenVpnPilot.Server.Auth.File;

public sealed class FileAuthProvider(UserFileStore store, IPasswordHasher hasher) : IAuthProvider
{
    public AuthProviderKind Kind => AuthProviderKind.File;

    public Task<ExternalIdentity> SignInAsync(string username, string? password, CancellationToken cancellationToken)
    {
        UserFileEntry? entry = store.Find(username);

        // An unknown name costs as much as a wrong password, so timing does not tell them apart.
        string stored = entry?.Password ?? DummyHash;
        bool valid = hasher.Verify(password ?? string.Empty, stored) && entry is not null && !string.IsNullOrEmpty(password);
        if (!valid)
        {
            throw ServiceException.Unauthorized(ErrorCodes.InvalidCredentials, "The user name or password is not correct.");
        }

        return Task.FromResult(new ExternalIdentity(
            entry!.Username, entry.DisplayName, null, RoleOf(entry), entry.Disabled));
    }

    public Task<ProviderAccountStatus> RecheckAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(QuickCheck(user.Username)!);

    public ProviderAccountStatus? QuickCheck(string username)
    {
        UserFileEntry? entry = store.Find(username);
        return entry switch
        {
            null => ProviderAccountStatus.Missing,
            { Disabled: true } => ProviderAccountStatus.Disabled,
            _ => ProviderAccountStatus.Active(RoleOf(entry)),
        };
    }

    private static UserRole RoleOf(UserFileEntry entry) =>
        string.Equals(entry.Role, "admin", StringComparison.OrdinalIgnoreCase) ? UserRole.Admin : UserRole.User;

    // A valid hash of a random value nobody knows, verified when the name does not exist.
    private const string DummyHash =
        "$argon2id$v=19$m=65536,t=3,p=1$c29tZXNhbHRzb21lc2FsdA$Wm9rR1v3yY1Jm8R2mJ3dGZ0b3JpZ2luYWxoYXNoMDA";
}
