namespace OpenVpnPilot.Server.Configuration;

public enum LdapSecurity
{
    Ldaps,
    StartTls,
}

public sealed record LdapOptions
{
    public required string Host { get; init; }

    public required int Port { get; init; }

    public required LdapSecurity Security { get; init; }

    // A directory with a private certificate authority is the usual case, so that authority can be
    // named instead of trusting every certificate.
    public string? CaCertificatePath { get; init; }

    public required string BindDn { get; init; }

    public required string BindPassword { get; init; }

    public required string BaseDn { get; init; }

    public required string UserFilter { get; init; }

    public required string DisplayNameAttribute { get; init; }

    public required string AdminGroupDn { get; init; }

    public string? UserGroupDn { get; init; }

    public required bool ActiveDirectory { get; init; }

    public static LdapOptions Read(EnvironmentReader env)
    {
        LdapSecurity security = env.Choice<LdapSecurity>("OVP_LDAP_SECURITY", LdapSecurity.Ldaps);
        bool activeDirectory = env.Switch("OVP_LDAP_ACTIVE_DIRECTORY", true);
        string defaultFilter = activeDirectory
            ? "(&(objectClass=user)(sAMAccountName={0}))"
            : "(&(objectClass=inetOrgPerson)(uid={0}))";

        LdapOptions options = new()
        {
            Host = env.Required("OVP_LDAP_HOST"),
            Port = env.WholeNumber("OVP_LDAP_PORT", security == LdapSecurity.Ldaps ? 636 : 389, 1, 65535),
            Security = security,
            CaCertificatePath = env.Optional("OVP_LDAP_CA_CERT_PATH"),
            BindDn = env.Required("OVP_LDAP_BIND_DN"),
            BindPassword = env.Required("OVP_LDAP_BIND_PASSWORD"),
            BaseDn = env.Required("OVP_LDAP_BASE_DN"),
            UserFilter = env.Text("OVP_LDAP_USER_FILTER", defaultFilter),
            DisplayNameAttribute = env.Text("OVP_LDAP_DISPLAY_NAME_ATTRIBUTE", activeDirectory ? "displayName" : "cn"),
            AdminGroupDn = env.Required("OVP_LDAP_ADMIN_GROUP"),
            UserGroupDn = env.Optional("OVP_LDAP_USER_GROUP"),
            ActiveDirectory = activeDirectory,
        };

        if (!options.UserFilter.Contains("{0}", StringComparison.Ordinal))
        {
            env.Fail("OVP_LDAP_USER_FILTER", "must contain {0} where the user name goes.");
        }

        if (options.CaCertificatePath is not null && !File.Exists(options.CaCertificatePath))
        {
            env.Fail("OVP_LDAP_CA_CERT_PATH", $"points at '{options.CaCertificatePath}', which does not exist.");
        }

        return options;
    }
}
