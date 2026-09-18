using OpenVpnPilot.Server.Configuration;
using OpenVpnPilot.Server.Data.Entities;

namespace OpenVpnPilot.Server.Auth.None;

// A name is all there is. Meant for a trusted network where the server is a shared library and not a
// gate; who administers it is decided by the environment, never by what a caller claims.
public sealed class NoneAuthProvider(AuthOptions options) : IAuthProvider
{
    public AuthProviderKind Kind => AuthProviderKind.None;

    public Task<ExternalIdentity> SignInAsync(string username, string? password, CancellationToken cancellationToken) =>
        Task.FromResult(new ExternalIdentity(username, null, null, RoleOf(username)));

    public Task<ProviderAccountStatus> RecheckAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(ProviderAccountStatus.Active(RoleOf(user.Username)));

    public ProviderAccountStatus? QuickCheck(string username) => ProviderAccountStatus.Active(RoleOf(username));

    private UserRole RoleOf(string username) =>
        options.NoneAdmins.Contains(username) ? UserRole.Admin : UserRole.User;
}
