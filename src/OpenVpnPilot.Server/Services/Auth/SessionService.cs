using OpenVpnPilot.Server.Auth;
using OpenVpnPilot.Server.Auth.Tokens;
using OpenVpnPilot.Server.Configuration;
using OpenVpnPilot.Server.Contracts;
using OpenVpnPilot.Server.Contracts.Responses;
using OpenVpnPilot.Server.Data.Entities;
using OpenVpnPilot.Server.Mapping;
using OpenVpnPilot.Server.Repositories.Interfaces;
using OpenVpnPilot.Server.Security;

namespace OpenVpnPilot.Server.Services.Auth;

public interface ISessionService
{
    public Task<TokenResponse> StartAsync(User user, Guid clientId, CancellationToken cancellationToken);

    public Task<TokenResponse> RefreshAsync(string refreshToken, Guid clientId, CancellationToken cancellationToken);

    public Task LogoutAsync(string refreshToken, Guid clientId, CancellationToken cancellationToken);
}

public sealed class SessionService(
    IRefreshTokenRepository tokens,
    IUnitOfWork unitOfWork,
    IUserAccountService accounts,
    IAuthProvider provider,
    AccessTokenIssuer issuer,
    ServerOptions options,
    TimeProvider time,
    ILogger<SessionService> logger) : ISessionService
{
    public async Task<TokenResponse> StartAsync(User user, Guid clientId, CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();
        TokenResponse response = Issue(user, clientId, Guid.NewGuid(), now, out _);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        AuthLog.SignedIn(logger, user.Username, user.Role, clientId);
        return response;
    }

    public async Task<TokenResponse> RefreshAsync(string refreshToken, Guid clientId, CancellationToken cancellationToken)
    {
        RefreshToken current = await tokens.FindByHashAsync(TokenHashing.Hash(refreshToken), cancellationToken)
            ?? throw Invalid("The refresh token is not known. Sign in again.");
        User user = current.User!;
        DateTimeOffset now = time.GetUtcNow();

        if (current.ClientId != clientId)
        {
            AuthLog.ClientMismatch(logger, user.Username, current.ClientId, clientId);
            throw ServiceException.Unauthorized(ErrorCodes.ClientMismatch, "This refresh token belongs to another installation.");
        }

        // Checked before anything else, so a removed account is told to wipe itself rather than to sign in again.
        if (user.State != UserState.Active)
        {
            AuthLog.RevokedUserRefused(logger, user.Username, user.State, user.StateSource);
            throw ServiceException.Revoked("This account has been disabled or removed. Everything this server provided must be erased.");
        }

        if (current.ReplacedById is not null)
        {
            // A token that was already exchanged is being used again: one of the two holders stole it.
            int revoked = await tokens.RevokeFamilyAsync(current.FamilyId, now, cancellationToken);
            AuthLog.RefreshTokenReused(logger, user.Username, clientId, revoked);
            throw ServiceException.Unauthorized(ErrorCodes.RefreshTokenReused, "The refresh token was already used. Sign in again.");
        }

        if (current.RevokedAt is not null || current.ExpiresAt <= now)
        {
            throw Invalid("The session has ended. Sign in again.");
        }

        await RecheckWithProviderAsync(user, current, now, cancellationToken);

        TokenResponse response = Issue(user, clientId, current.FamilyId, current.AuthenticatedAt, out RefreshToken next);
        current.ReplacedById = next.Id;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        AuthLog.Refreshed(logger, user.Username, clientId);
        return response;
    }

    public async Task LogoutAsync(string refreshToken, Guid clientId, CancellationToken cancellationToken)
    {
        RefreshToken? current = await tokens.FindByHashAsync(TokenHashing.Hash(refreshToken), cancellationToken);
        if (current is null || current.ClientId != clientId)
        {
            // Signing out of a session that does not exist leaves the same state as signing out of one that does.
            return;
        }

        int revoked = await tokens.RevokeFamilyAsync(current.FamilyId, time.GetUtcNow(), cancellationToken);
        AuthLog.SignedOut(logger, current.User!.Username, clientId, revoked);
    }

    private async Task RecheckWithProviderAsync(User user, RefreshToken current, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (user.Provider != provider.Kind)
        {
            // The server was switched to another way of signing in. Nothing is known about this user in
            // the new one, and that is no reason to erase anything.
            throw ServiceException.Unauthorized(ErrorCodes.ReauthenticationRequired, "This server now signs in differently. Sign in again.");
        }

        if (options.Auth.Entra is { } entra && now - current.AuthenticatedAt > entra.ReauthenticateAfter)
        {
            throw ServiceException.Unauthorized(ErrorCodes.ReauthenticationRequired, "Sign in with Entra ID again to continue.");
        }

        ProviderAccountStatus status = await provider.RecheckAsync(user, cancellationToken);
        await accounts.ApplyAsync(user, status, cancellationToken);
    }

    private TokenResponse Issue(User user, Guid clientId, Guid familyId, DateTimeOffset authenticatedAt, out RefreshToken stored)
    {
        DateTimeOffset now = time.GetUtcNow();
        string refresh = TokenHashing.NewToken();
        stored = new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            FamilyId = familyId,
            TokenHash = TokenHashing.Hash(refresh),
            ClientId = clientId,
            CreatedAt = now,
            ExpiresAt = now + options.Security.RefreshTokenLifetime,
            AuthenticatedAt = authenticatedAt,
        };
        tokens.Add(stored);

        IssuedAccessToken access = issuer.Issue(user, clientId);
        return new TokenResponse(access.Token, access.ExpiresAt, refresh, stored.ExpiresAt, user.ToCurrentUser());
    }

    private static ServiceException Invalid(string detail) =>
        ServiceException.Unauthorized(ErrorCodes.RefreshTokenInvalid, detail);
}
