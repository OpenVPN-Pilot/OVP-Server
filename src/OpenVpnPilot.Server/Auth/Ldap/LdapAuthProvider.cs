using Novell.Directory.Ldap;
using OpenVpnPilot.Server.Configuration;
using OpenVpnPilot.Server.Contracts;
using OpenVpnPilot.Server.Data.Entities;
using OpenVpnPilot.Server.Services;

namespace OpenVpnPilot.Server.Auth.Ldap;

public sealed class LdapAuthProvider(
    LdapConnector connector,
    LdapDirectory directory,
    AuthOptions auth,
    ILogger<LdapAuthProvider> logger) : IAuthProvider
{
    private readonly LdapOptions options = auth.Ldap ?? throw new InvalidOperationException("LDAP options are missing.");

    public AuthProviderKind Kind => AuthProviderKind.Ldap;

    public async Task<ExternalIdentity> SignInAsync(string username, string? password, CancellationToken cancellationToken)
    {
        // An empty password is an anonymous bind, which most directories accept. It proves nothing.
        if (string.IsNullOrEmpty(password))
        {
            throw InvalidCredentials();
        }

        using LdapConnection service = await OpenServiceAsync();
        LdapUserEntry user = await directory.FindUserAsync(service, username) ?? throw InvalidCredentials();
        if (user.Disabled)
        {
            throw InvalidCredentials();
        }

        await VerifyPasswordAsync(user.Dn, password);

        // Proven, but not in the group allowed in: reported as disabled, so someone who lost the group
        // is treated like any other revoked account.
        UserRole? role = await RoleOfAsync(service, user.Dn);
        return new ExternalIdentity(username, user.DisplayName, user.Dn, role ?? UserRole.User, Disabled: role is null);
    }

    public async Task<ProviderAccountStatus> RecheckAsync(User user, CancellationToken cancellationToken)
    {
        using LdapConnection service = await OpenServiceAsync();
        LdapUserEntry? entry = await directory.FindUserAsync(service, user.Username);
        if (entry is null)
        {
            return ProviderAccountStatus.Missing;
        }

        if (entry.Disabled)
        {
            return ProviderAccountStatus.Disabled;
        }

        UserRole? role = await RoleOfAsync(service, entry.Dn);
        return role is null ? ProviderAccountStatus.Disabled : ProviderAccountStatus.Active(role.Value);
    }

    public ProviderAccountStatus? QuickCheck(string username) => null;

    private async Task<UserRole?> RoleOfAsync(LdapConnection service, string userDn)
    {
        if (await directory.IsMemberAsync(service, userDn, options.AdminGroupDn))
        {
            return UserRole.Admin;
        }

        if (options.UserGroupDn is not null && !await directory.IsMemberAsync(service, userDn, options.UserGroupDn))
        {
            return null;
        }

        return UserRole.User;
    }

    private async Task VerifyPasswordAsync(string userDn, string password)
    {
        try
        {
            using LdapConnection connection = await connector.OpenAsync();
            await connection.BindAsync(userDn, password);
        }
        catch (LdapException exception) when (exception.ResultCode == LdapException.InvalidCredentials)
        {
            throw InvalidCredentials();
        }
    }

    private async Task<LdapConnection> OpenServiceAsync()
    {
        try
        {
            return await connector.OpenAsServiceAsync();
        }
        catch (Exception exception) when (exception is LdapException or System.Security.Authentication.AuthenticationException
            or IOException or System.Net.Sockets.SocketException)
        {
            // A refused certificate lands here too, and the log line carries the reason.
            AuthLog.DirectoryUnavailable(logger, options.Host, options.Port, exception);
            throw ServiceException.Unavailable(ErrorCodes.ProviderUnavailable, "The directory cannot be reached. Try again shortly.");
        }
    }

    private static ServiceException InvalidCredentials() =>
        ServiceException.Unauthorized(ErrorCodes.InvalidCredentials, "The user name or password is not correct.");
}
