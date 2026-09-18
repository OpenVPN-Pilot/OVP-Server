namespace OpenVpnPilot.Server.Configuration;

public sealed record EntraOptions
{
    public required string TenantId { get; init; }

    // The application registration the desktop client signs in with. Published to clients unchanged.
    public required string ClientId { get; init; }

    // Every audience an access token for this server may carry.
    public required IReadOnlyList<string> Audiences { get; init; }

    // The full scope a client requests, for example api://<id>/access_as_user.
    public required string Scope { get; init; }

    public required string AdminRole { get; init; }

    public required string UserRole { get; init; }

    public string? AdminGroupId { get; init; }

    public required bool RequireRole { get; init; }

    // Entra cannot be asked whether an account still exists without a Graph permission, so a session
    // is sent back to Entra this often and a disabled account is noticed there.
    public required TimeSpan ReauthenticateAfter { get; init; }

    public string RequiredScopeName => Scope[(Scope.LastIndexOf('/') + 1)..];

    public static EntraOptions Read(EnvironmentReader env)
    {
        string clientId = env.Required("OVP_ENTRA_CLIENT_ID");
        string audience = env.Text("OVP_ENTRA_AUDIENCE", $"api://{clientId}");

        return new EntraOptions
        {
            TenantId = env.Required("OVP_ENTRA_TENANT_ID"),
            ClientId = clientId,
            Audiences = audience.StartsWith("api://", StringComparison.Ordinal)
                ? [audience, audience["api://".Length..]]
                : [audience, $"api://{audience}"],
            Scope = env.Text("OVP_ENTRA_SCOPE", $"{audience}/access_as_user"),
            AdminRole = env.Text("OVP_ENTRA_ADMIN_ROLE", "Admin"),
            UserRole = env.Text("OVP_ENTRA_USER_ROLE", "User"),
            AdminGroupId = env.Optional("OVP_ENTRA_ADMIN_GROUP"),
            RequireRole = env.Switch("OVP_ENTRA_REQUIRE_ROLE", false),
            ReauthenticateAfter = TimeSpan.FromHours(env.WholeNumber("OVP_ENTRA_REAUTH_HOURS", 8, 1, 720)),
        };
    }
}
