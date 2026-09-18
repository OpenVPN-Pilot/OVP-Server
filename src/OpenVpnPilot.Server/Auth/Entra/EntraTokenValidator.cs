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
        OpenIdConnectConfiguration configuration;
        try
        {
            configuration = await metadata.GetConfigurationAsync(cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            // Entra's signing keys could not be fetched: no network, a proxy in the way, or a wrong tenant.
            AuthLog.EntraMetadataUnavailable(logger, options.TenantId, exception);
            throw ServiceException.Unavailable(ErrorCodes.ProviderUnavailable, "Entra ID cannot be reached. Try again shortly.");
        }

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

        TokenValidationResult result = await new JsonWebTokenHandler().ValidateTokenAsync(accessToken, parameters);
        if (!result.IsValid)
        {
            string reason = result.Exception?.Message ?? "unknown reason";
            AuthLog.EntraTokenRejected(logger, reason);
            throw ServiceException.Unauthorized(ErrorCodes.InvalidCredentials, "The Entra ID token was not accepted.");
        }

        return ReadIdentity(result.ClaimsIdentity);
    }

    private ExternalIdentity ReadIdentity(ClaimsIdentity identity)
    {
        string[] scopes = (identity.FindFirst("scp")?.Value ?? string.Empty).Split(' ');
        if (!scopes.Contains(options.RequiredScopeName, StringComparer.Ordinal))
        {
            throw ServiceException.Unauthorized(
                ErrorCodes.InvalidCredentials, $"The Entra ID token does not carry the scope '{options.RequiredScopeName}'.");
        }

        string objectId = identity.FindFirst("oid")?.Value
            ?? throw ServiceException.Unauthorized(ErrorCodes.InvalidCredentials, "The Entra ID token names no user.");
        string username = identity.FindFirst("preferred_username")?.Value
            ?? identity.FindFirst("upn")?.Value
            ?? identity.FindFirst("unique_name")?.Value
            ?? objectId;

        UserRole role = RoleOf(identity)
            ?? throw ServiceException.Forbidden(ErrorCodes.Forbidden, "This account has no role for this server.");
        return new ExternalIdentity(UsernameRules.Normalise(username), identity.FindFirst("name")?.Value, objectId, role);
    }

    private UserRole? RoleOf(ClaimsIdentity identity)
    {
        HashSet<string> roles = identity.FindAll("roles").Select(c => c.Value).ToHashSet(StringComparer.Ordinal);
        bool inAdminGroup = options.AdminGroupId is not null
            && identity.FindAll("groups").Any(c => string.Equals(c.Value, options.AdminGroupId, StringComparison.OrdinalIgnoreCase));

        if (roles.Contains(options.AdminRole) || inAdminGroup)
        {
            return UserRole.Admin;
        }

        if (roles.Contains(options.UserRole) || !options.RequireRole)
        {
            return UserRole.User;
        }

        return null;
    }
}
