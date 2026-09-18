namespace OpenVpnPilot.Server.Configuration;

public sealed record ServerOptions
{
    public required DatabaseOptions Database { get; init; }

    public required SecurityOptions Security { get; init; }

    public required TlsOptions Tls { get; init; }

    public required AuthOptions Auth { get; init; }

    public required LoggingOptions Logging { get; init; }

    public required ApiOptions Api { get; init; }

    public static ServerOptions Load(IConfiguration configuration)
    {
        EnvironmentReader env = new(configuration);
        ServerOptions options = new()
        {
            Database = DatabaseOptions.Read(env),
            Security = SecurityOptions.Read(env),
            Tls = TlsOptions.Read(env),
            Auth = AuthOptions.Read(env),
            Logging = LoggingOptions.Read(env),
            Api = ApiOptions.Read(env),
        };

        if (env.Errors.Count > 0)
        {
            throw new ConfigurationException(env.Errors);
        }

        return options;
    }
}
