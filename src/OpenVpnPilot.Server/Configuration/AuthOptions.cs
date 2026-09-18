namespace OpenVpnPilot.Server.Configuration;

public enum AuthMode
{
    None,
    File,
    Ldap,
    Entra,
}

public sealed record AuthOptions
{
    public required AuthMode Mode { get; init; }

    // Only read in mode none, where a name is all there is and the environment decides who administers.
    public required IReadOnlySet<string> NoneAdmins { get; init; }

    public required string UserFilePath { get; init; }

    public required int LoginAttemptsPerMinute { get; init; }

    public LdapOptions? Ldap { get; init; }

    public EntraOptions? Entra { get; init; }

    public static AuthOptions Read(EnvironmentReader env)
    {
        AuthMode mode = env.Choice<AuthMode>("OVP_AUTH_MODE", null);
        AuthOptions options = new()
        {
            Mode = mode,
            NoneAdmins = env.List("OVP_AUTH_NONE_ADMINS").ToHashSet(StringComparer.OrdinalIgnoreCase),
            UserFilePath = env.Text("OVP_AUTH_FILE", "/app/config/users.yaml"),
            LoginAttemptsPerMinute = env.WholeNumber("OVP_LOGIN_ATTEMPTS_PER_MINUTE", 10, 1, 1000),
            Ldap = mode == AuthMode.Ldap ? LdapOptions.Read(env) : null,
            Entra = mode == AuthMode.Entra ? EntraOptions.Read(env) : null,
        };

        if (mode == AuthMode.File && !System.IO.File.Exists(options.UserFilePath))
        {
            env.Fail("OVP_AUTH_FILE", $"points at '{options.UserFilePath}', which does not exist.");
        }

        return options;
    }
}
