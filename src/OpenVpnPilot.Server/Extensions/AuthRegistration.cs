using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using OpenVpnPilot.Server.Auth;
using OpenVpnPilot.Server.Auth.Entra;
using OpenVpnPilot.Server.Auth.File;
using OpenVpnPilot.Server.Auth.Ldap;
using OpenVpnPilot.Server.Auth.None;
using OpenVpnPilot.Server.Auth.Tokens;
using OpenVpnPilot.Server.Configuration;
using OpenVpnPilot.Server.Contracts;
using OpenVpnPilot.Server.Middleware;

namespace OpenVpnPilot.Server.Extensions;

public static class AuthRegistration
{
    public static IServiceCollection AddPilotAuthentication(this IServiceCollection services, ServerOptions options)
    {
        services.AddSingleton<AccessTokenIssuer>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        AddProvider(services, options.Auth.Mode);

        SymmetricSecurityKey signingKey = new(options.Security.JwtSigningKey);
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = PilotClaims.Issuer,
                    ValidAudience = PilotClaims.Audience,
                    IssuerSigningKey = signingKey,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = PilotClaims.Name,
                    RoleClaimType = PilotClaims.Role,
                };
                jwt.Events = new JwtBearerEvents { OnChallenge = ChallengeAsync, OnForbidden = ForbiddenAsync };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(AuthorizationPolicies.Admin, policy => policy.RequireClaim(PilotClaims.Role, PilotClaims.AdminRole));

        services.AddRateLimiter(limiter =>
        {
            limiter.AddPolicy(AuthorizationPolicies.SignInRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = options.Auth.LoginAttemptsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
            limiter.OnRejected = RejectedAsync;
        });

        return services;
    }

    private static void AddProvider(IServiceCollection services, AuthMode mode)
    {
        switch (mode)
        {
            case AuthMode.None:
                services.AddSingleton<IAuthProvider, NoneAuthProvider>();
                break;
            case AuthMode.File:
                services.AddSingleton<UserFileStore>().AddSingleton<IAuthProvider, FileAuthProvider>();
                break;
            case AuthMode.Ldap:
                services.AddSingleton<LdapConnector>().AddSingleton<LdapDirectory>().AddSingleton<IAuthProvider, LdapAuthProvider>();
                break;
            case AuthMode.Entra:
                services.AddSingleton<EntraTokenValidator>().AddSingleton<IAuthProvider, EntraAuthProvider>();
                break;
            default:
                throw new ConfigurationException([$"OVP_AUTH_MODE {mode} has no provider."]);
        }
    }

    private static Task ChallengeAsync(JwtBearerChallengeContext context)
    {
        context.HandleResponse();
        (string code, string detail) = context.AuthenticateFailure switch
        {
            SecurityTokenExpiredException => (ErrorCodes.TokenExpired, "The access token has expired. Refresh the session."),
            not null => (ErrorCodes.TokenInvalid, "The access token is not valid."),
            null => (ErrorCodes.TokenMissing, "This endpoint needs an access token in the Authorization header."),
        };
        return ProblemResponses.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized, code, detail);
    }

    private static Task ForbiddenAsync(ForbiddenContext context) =>
        ProblemResponses.WriteAsync(
            context.HttpContext, StatusCodes.Status403Forbidden, ErrorCodes.Forbidden, "Only administrators may do this.");

    private static async ValueTask RejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        HttpContext http = context.HttpContext;
        ILogger logger = http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AuthRegistration));
        PipelineLog.RateLimited(logger, http.Request.Method, http.Request.Path, http.Connection.RemoteIpAddress);
        http.Response.Headers.RetryAfter = "60";
        await ProblemResponses.WriteAsync(
            http, StatusCodes.Status429TooManyRequests, ErrorCodes.TooManyRequests, "Too many attempts. Wait a minute and try again.");
    }
}
