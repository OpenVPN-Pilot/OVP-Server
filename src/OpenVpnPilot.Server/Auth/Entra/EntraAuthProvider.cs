using OpenVpnPilot.Server.Contracts;
using OpenVpnPilot.Server.Data.Entities;
using OpenVpnPilot.Server.Services;

namespace OpenVpnPilot.Server.Auth.Entra;

// Passwords never reach this server in Entra mode; the client signs in with Entra and exchanges the
// token. Whether the account still exists is settled by sending the client back to Entra periodically.
public sealed class EntraAuthProvider : IAuthProvider
{
    public AuthProviderKind Kind => AuthProviderKind.Entra;

    public Task<ExternalIdentity> SignInAsync(string username, string? password, CancellationToken cancellationToken) =>
        throw ServiceException.BadRequest(
            ErrorCodes.ModeMismatch, "This server signs in with Entra ID. Use POST /api/v1/auth/entra/exchange.");

    public Task<ProviderAccountStatus> RecheckAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(ProviderAccountStatus.Active(user.Role));

    public ProviderAccountStatus? QuickCheck(string username) => null;
}
