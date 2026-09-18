namespace OpenVpnPilot.Server.Configuration;

public sealed record SecurityOptions
{
    // Encrypts profile configurations and vault entries at rest.
    public required byte[] DataKey { get; init; }

    public required byte[] JwtSigningKey { get; init; }

    public required TimeSpan AccessTokenLifetime { get; init; }

    public required TimeSpan RefreshTokenLifetime { get; init; }

    public static SecurityOptions Read(EnvironmentReader env) => new()
    {
        DataKey = env.Key("OVP_DATA_KEY", 32, exactBytes: 32),
        JwtSigningKey = env.Key("OVP_JWT_SIGNING_KEY", 32),
        AccessTokenLifetime = TimeSpan.FromMinutes(env.WholeNumber("OVP_ACCESS_TOKEN_MINUTES", 15, 1, 1440)),
        RefreshTokenLifetime = TimeSpan.FromDays(env.WholeNumber("OVP_REFRESH_TOKEN_DAYS", 30, 1, 365)),
    };
}
