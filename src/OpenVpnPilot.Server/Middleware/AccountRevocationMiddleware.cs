using System.Security.Claims;
using OpenVpnPilot.Server.Auth;
using OpenVpnPilot.Server.Auth.Tokens;
using OpenVpnPilot.Server.Contracts;
using OpenVpnPilot.Server.Data.Entities;
using OpenVpnPilot.Server.Repositories.Interfaces;
using OpenVpnPilot.Server.Services;
using OpenVpnPilot.Server.Services.Auth;
using OpenVpnPilot.Server.Services.Users;
using Serilog;
using Serilog.Context;

namespace OpenVpnPilot.Server.Middleware;

// An access token is valid until it expires, whatever happened to the account in the meantime. This
// closes that gap: every authenticated request is checked against the account as it is now, and a
// client whose account is gone is told to erase what it holds from this server.
public sealed class AccountRevocationMiddleware(RequestDelegate next, ILogger<AccountRevocationMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        IUserStateService states,
        IAuthProvider provider,
        IUserAccountService accounts,
        IUserRepository users,
        IDiagnosticContext diagnostics)
    {
        ClaimsPrincipal principal = context.User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        Guid userId = Guid.Parse(principal.FindFirst(PilotClaims.Subject)!.Value);
        CancellationToken cancellationToken = context.RequestAborted;
        UserSnapshot snapshot = await CurrentStateAsync(userId, states, provider, accounts, users, cancellationToken);

        if (principal.FindFirst(PilotClaims.SecurityStamp)?.Value != snapshot.SecurityStamp.ToString())
        {
            AuthLog.StaleTokenRefused(logger, snapshot.Username);
            throw ServiceException.Unauthorized(ErrorCodes.TokenRevoked, "This access token was revoked. Refresh the session.");
        }

        string? tokenClient = principal.FindFirst(PilotClaims.ClientId)?.Value;
        Guid presentedBy = context.GetClientContext().ClientId;
        if (tokenClient != presentedBy.ToString())
        {
            AuthLog.ClientMismatch(logger, snapshot.Username, tokenClient, presentedBy);
            throw ServiceException.Unauthorized(ErrorCodes.ClientMismatch, "This access token belongs to another installation.");
        }

        await states.TouchAsync(userId, cancellationToken);
        diagnostics.Set("UserName", snapshot.Username);
        using (LogContext.PushProperty("UserName", snapshot.Username))
        {
            await next(context);
        }
    }

    private async Task<UserSnapshot> CurrentStateAsync(
        Guid userId,
        IUserStateService states,
        IAuthProvider provider,
        IUserAccountService accounts,
        IUserRepository users,
        CancellationToken cancellationToken)
    {
        UserSnapshot? snapshot = await states.GetAsync(userId, cancellationToken);
        if (snapshot is null)
        {
            AuthLog.UnknownUserRefused(logger, userId);
            throw ServiceException.Revoked("This account no longer exists. Everything this server provided must be erased.");
        }

        if (snapshot.State != UserState.Active)
        {
            AuthLog.RevokedUserRefused(logger, snapshot.Username, snapshot.State, null);
            throw ServiceException.Revoked("This account has been disabled or removed. Everything this server provided must be erased.");
        }

        // A provider that can answer at once, such as the user file, takes effect on this very request.
        ProviderAccountStatus? quick = snapshot.Provider == provider.Kind ? provider.QuickCheck(snapshot.Username) : null;
        if (quick is not null && (quick.Kind != AccountStatusKind.Active || quick.Role != snapshot.Role))
        {
            User user = await users.FindAsync(userId, cancellationToken)
                ?? throw ServiceException.Revoked("This account no longer exists. Everything this server provided must be erased.");
            await accounts.ApplyAsync(user, quick, cancellationToken);
            snapshot = await states.GetAsync(userId, cancellationToken) ?? snapshot;
        }

        return snapshot;
    }
}
