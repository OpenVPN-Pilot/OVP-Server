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

        return await AgainstDirectoryAsync(async () =>
        {
            using LdapConnection service = await connector.OpenAsServiceAsync(cancellationToken);
            LdapUserLookup lookup = await directory.FindUserAsync(service, username, cancellationToken);
            if (lookup.Match == LdapMatch.Ambiguous)
            {
                AuthLog.DirectoryAmbiguousUser(logger, username, lookup.Count);
            }

            if (lookup.Entry is not { Disabled: false } user)
            {
                throw InvalidCredentials();
            }

            await VerifyPasswordAsync(user.Dn, password, cancellationToken);

            // Proven, but not in the group allowed in: reported as disabled, so someone who lost the group
            // is treated like any other revoked account.
            UserRole? role = await RoleOfAsync(service, user.Dn, cancellationToken);
            return new ExternalIdentity(username, user.DisplayName, user.Dn, role ?? UserRole.User, Disabled: role is null);
        });
    }

    public Task<ProviderAccountStatus> RecheckAsync(User user, CancellationToken cancellationToken) =>
        AgainstDirectoryAsync(async () =>
        {
            using LdapConnection service = await connector.OpenAsServiceAsync(cancellationToken);
            LdapUserLookup lookup = await directory.FindUserAsync(service, user.Username, cancellationToken);
            switch (lookup.Match)
            {
                case LdapMatch.NotFound:
                    return ProviderAccountStatus.Missing;
                case LdapMatch.Ambiguous:
                    // A second entry that matches the filter says nothing about whether this person left,
                    // so it must not erase their clients.
                    AuthLog.DirectoryAmbiguousUser(logger, user.Username, lookup.Count);
                    throw ServiceException.Unavailable(
                        ErrorCodes.ProviderUnavailable, "The directory cannot confirm this account right now. Try again later.");
            }

            LdapUserEntry entry = lookup.Entry!;
            if (entry.Disabled)
            {
                return ProviderAccountStatus.Disabled;
            }

            UserRole? role = await RoleOfAsync(service, entry.Dn, cancellationToken);
            return role is null ? ProviderAccountStatus.Disabled : ProviderAccountStatus.Active(role.Value);
        });

    public ProviderAccountStatus? QuickCheck(string username) => null;

    private async Task<UserRole?> RoleOfAsync(LdapConnection service, string userDn, CancellationToken cancellationToken)
    {
        if (await directory.IsMemberAsync(service, userDn, options.AdminGroupDn, "OVP_LDAP_ADMIN_GROUP", cancellationToken))
        {
            return UserRole.Admin;
        }

        if (options.UserGroupDn is not null
            && !await directory.IsMemberAsync(service, userDn, options.UserGroupDn, "OVP_LDAP_USER_GROUP", cancellationToken))
        {
            return null;
        }

        return UserRole.User;
    }

    private async Task VerifyPasswordAsync(string userDn, string password, CancellationToken cancellationToken)
    {
        try
        {
            using LdapConnection connection = await connector.OpenAsync(cancellationToken);
            await connection.BindAsync(userDn, password, cancellationToken);
        }
        catch (LdapException exception) when (exception.ResultCode == LdapException.InvalidCredentials)
        {
            throw InvalidCredentials();
        }
    }

    // Everything that talks to the directory goes through here, so a directory that fails halfway, not
    // only one that refuses the first connection, leaves the client waiting rather than with a fault.
    private async Task<T> AgainstDirectoryAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (LdapGroupMissingException exception)
        {
            AuthLog.DirectoryGroupMissing(logger, exception.Variable, exception.GroupDn);
            throw ServiceException.Unavailable(
                ErrorCodes.ProviderUnavailable, "The directory is not set up as this server expects. Try again later.");
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
