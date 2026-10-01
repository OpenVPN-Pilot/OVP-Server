using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using OpenVpnPilot.Server.Configuration;
using OpenVpnPilot.Server.Contracts;
using OpenVpnPilot.Server.Data.Entities;
using OpenVpnPilot.Server.Services;

namespace OpenVpnPilot.Server.Auth.Entra;

// Checks an access token the client obtained from Entra ID for this server, and reads who it belongs to.
// The server never talks to Entra on anyone's behalf; it only fetches Entra's published signing keys.
public sealed class EntraTokenValidator
{
    private readonly EntraOptions options;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> metadata;
    private readonly ILogger<EntraTokenValidator> logger;

    public EntraTokenValidator(AuthOptions auth, ILogger<EntraTokenValidator> logger)
    {
        options = auth.Entra ?? throw new InvalidOperationException("Entra options are missing.");
        this.logger = logger;
        metadata = new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{options.Authority}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = true });
    }

    public async Task<ExternalIdentity> ValidateAsync(string accessToken, CancellationToken cancellationToken)
    {
        TokenValidationResult result = await ValidateAgainstAsync(accessToken, await MetadataAsync(cancellationToken));
        if (result.Exception is SecurityTokenSignatureKeyNotFoundException)
        {
            // Entra rotates its signing keys. A token signed with a key published after the cached list was
            // fetched is checked once more against a fresh list instead of being refused for hours.
            metadata.RequestRefresh();
            result = await ValidateAgainstAsync(accessToken, await MetadataAsync(cancellationToken));
        }

        if (!result.IsValid)
        {
            string reason = result.Exception?.Message ?? "unknown reason";
            AuthLog.EntraTokenRejected(logger, reason);
            throw ServiceException.Unauthorized(ErrorCodes.InvalidCredentials, "The Entra ID token was not accepted.");
        }

        return ReadIdentity(result.ClaimsIdentity);
    }

    private async Task<OpenIdConnectConfiguration> MetadataAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await metadata.GetConfigurationAsync(cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            // Entra's signing keys could not be fetched: no network, a proxy in the way, or a wrong tenant.
            AuthLog.EntraMetadataUnavailable(logger, options.TenantId, exception);
            throw ServiceException.Unavailable(ErrorCodes.ProviderUnavailable, "Entra ID cannot be reached. Try again shortly.");
        }
    }

    private Task<TokenValidationResult> ValidateAgainstAsync(string accessToken, OpenIdConnectConfiguration configuration)
    {
        TokenValidationParameters parameters = new()
        {
            // Entra issues version 1 tokens unless the registration asks for version 2; both are accepted. A
            // version 1 token names the global security token service even for tenants of a national cloud.
            ValidIssuers =
            [
                options.Authority,
                $"https://sts.windows.net/{options.TenantId}/",
            ],
            ValidAudiences = options.Audiences,
            IssuerSigningKeys = configuration.SigningKeys,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
        };

        return new JsonWebTokenHandler().ValidateTokenAsync(accessToken, parameters);
    }

    private ExternalIdentity ReadIdentity(ClaimsIdentity identity)
    {
        string[] scopes = (identity.FindFirst("scp")?.Value ?? string.Empty).Split(' ');
        if (!scopes.Contains(options.RequiredScopeName, StringComparer.Ordinal))
        {
            throw ServiceException.Unauthorized(
                ErrorCodes.InvalidCredentials, $"The Entra ID token does not carry the scope '{options.RequiredScopeName}'.");
        }

        // The audience says the token is for this server; this says which application asked for it. Any
        // other application in the tenant that was granted the scope could otherwise sign people in here.
        string? requestedBy = identity.FindFirst("azp")?.Value ?? identity.FindFirst("appid")?.Value;
        if (requestedBy is not null && !string.Equals(requestedBy, options.ClientId, StringComparison.OrdinalIgnoreCase))
        {
            AuthLog.EntraWrongApplication(logger, requestedBy, options.ClientId);
            throw ServiceException.Unauthorized(ErrorCodes.InvalidCredentials, "The Entra ID token was issued to another application.");
        }

        string objectId = identity.FindFirst("oid")?.Value
            ?? throw ServiceException.Unauthorized(ErrorCodes.InvalidCredentials, "The Entra ID token names no user.");
        string username = identity.FindFirst("preferred_username")?.Value
            ?? identity.FindFirst("upn")?.Value
            ?? identity.FindFirst("unique_name")?.Value
            ?? objectId;

        // Without a role or group where one is required the person is reported as disabled: someone who
        // lost access is then treated like any revoked account, and someone new is simply refused.
        UserRole? role = RoleOf(identity);
        return new ExternalIdentity(
            UsernameRules.Normalise(username), identity.FindFirst("name")?.Value, objectId, role ?? UserRole.User, Disabled: role is null);
    }

    private UserRole? RoleOf(ClaimsIdentity identity)
    {
        HashSet<string> roles = identity.FindAll("roles").Select(c => c.Value).ToHashSet(StringComparer.Ordinal);
        HashSet<string> groups = identity.FindAll("groups").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (roles.Contains(options.AdminRole) || (options.AdminGroupId is not null && groups.Contains(options.AdminGroupId)))
        {
            return UserRole.Admin;
        }

        bool member = roles.Contains(options.UserRole) || (options.UserGroupId is not null && groups.Contains(options.UserGroupId));
        return member || !options.AccessIsRestricted ? UserRole.User : null;
    }
}