namespace OpenVpnPilot.Server.Configuration;

public sealed record ApiOptions
{
    public required bool SwaggerEnabled { get; init; }

    public required Version MinimumClientVersion { get; init; }

    public required TimeSpan ClockSkew { get; init; }

    public static ApiOptions Read(EnvironmentReader env)
    {
        string minimum = env.Text("OVP_MIN_CLIENT_VERSION", "0.0.0");
        if (!Version.TryParse(minimum, out Version? version))
        {
            env.Fail("OVP_MIN_CLIENT_VERSION", $"must be a version such as 1.9.0, was '{minimum}'.");
            version = new Version(0, 0, 0);
        }

        return new ApiOptions
        {
            SwaggerEnabled = env.Switch("OVP_SWAGGER_ENABLED", true),
            MinimumClientVersion = version,
            ClockSkew = TimeSpan.FromSeconds(env.WholeNumber("OVP_CLOCK_SKEW_SECONDS", 300, 5, 86400)),
        };
    }
}
