namespace OpenVpnPilot.Server.Configuration;

public sealed record EntraOptions
{
    // The Microsoft identity platform of the cloud the tenant lives in: the global one, or a national
    // cloud such as https://login.microsoftonline.us.
    public required string Instance { get; init; }

    public required string TenantId { get; init; }

    public string Authority => $"{Instance}/{TenantId}/v2.0";

    // The application registration the desktop client signs in with. Published to clients unchanged.
    public required string ClientId { get; init; }

    // Every audience an access token for this server may carry.
    public required IReadOnlyList<string> Audiences { get; init; }

    // The full scope a client requests, for example api://<id>/access_as_user.
    public required string Scope { get; init; }

    public required string AdminRole { get; init; }

    public required string UserRole { get; init; }

    public string? AdminGroupId { get; init; }

    // When set, only members of this group, holders of a role, or administrators may sign in at all.
    public string? UserGroupId { get; init; }

    public required bool RequireRole { get; init; }

    // Whether someone with neither a role nor a group is turned away rather than let in as a user.
    public bool AccessIsRestricted => RequireRole || UserGroupId is not null;

    // Entra cannot be asked whether an account still exists without a Graph permission, so a session
    // is sent back to Entra this often and a disabled account is noticed there.
    public required TimeSpan ReauthenticateAfter { get; init; }

    public string RequiredScopeName => Scope[(Scope.LastIndexOf('/') + 1)..];

    public static EntraOptions Read(EnvironmentReader env)
    {
        string clientId = env.Required("OVP_ENTRA_CLIENT_ID");
        string audience = env.Text("OVP_ENTRA_AUDIENCE", $"api://{clientId}");
        string instance = env.Text("OVP_ENTRA_INSTANCE", "https://login.microsoftonline.com").TrimEnd('/');
        if (!Uri.TryCreate(instance, UriKind.Absolute, out Uri? instanceUri) || instanceUri.Scheme != Uri.UriSchemeHttps)
        {
            env.Fail("OVP_ENTRA_INSTANCE", $"must be an https URL such as https://login.microsoftonline.com, was '{instance}'.");
        }

        // The issuer of every token names the tenant by its id, so a domain name here would match none.
        string tenantId = env.Required("OVP_ENTRA_TENANT_ID");
        if (tenantId.Length > 0 && !Guid.TryParse(tenantId, out _))
        {
            env.Fail("OVP_ENTRA_TENANT_ID", $"must be the directory (tenant) id, a GUID, not '{tenantId}'.");
        }

        return new EntraOptions
        {
            Instance = instance,
            TenantId = tenantId,
            ClientId = clientId,
            Audiences = audience.StartsWith("api://", StringComparison.Ordinal)
                ? [audience, audience["api://".Length..]]
                : [audience, $"api://{audience}"],
            Scope = env.Text("OVP_ENTRA_SCOPE", $"{audience}/access_as_user"),
            AdminRole = env.Text("OVP_ENTRA_ADMIN_ROLE", "Admin"),
            UserRole = env.Text("OVP_ENTRA_USER_ROLE", "User"),
            AdminGroupId = env.Optional("OVP_ENTRA_ADMIN_GROUP"),
            UserGroupId = env.Optional("OVP_ENTRA_USER_GROUP"),
            RequireRole = env.Switch("OVP_ENTRA_REQUIRE_ROLE", false),
            ReauthenticateAfter = TimeSpan.FromHours(env.WholeNumber("OVP_ENTRA_REAUTH_HOURS", 8, 1, 720)),
        };
    }
}
