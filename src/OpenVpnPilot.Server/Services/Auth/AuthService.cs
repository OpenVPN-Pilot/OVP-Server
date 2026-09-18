using OpenVpnPilot.Server.Auth;
using OpenVpnPilot.Server.Auth.Entra;
using OpenVpnPilot.Server.Contracts;
using OpenVpnPilot.Server.Contracts.Requests;
using OpenVpnPilot.Server.Contracts.Responses;
using OpenVpnPilot.Server.Data.Entities;
using OpenVpnPilot.Server.Mapping;
using OpenVpnPilot.Server.Repositories.Interfaces;

namespace OpenVpnPilot.Server.Services.Auth;

public interface IAuthService
{
    public Task<TokenResponse> LoginAsync(LoginRequest request, Guid clientId, CancellationToken cancellationToken);

    public Task<TokenResponse> ExchangeEntraAsync(EntraExchangeRequest request, Guid clientId, CancellationToken cancellationToken);

    public Task<CurrentUserResponse> MeAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed class AuthService(
    IAuthProvider provider,
    IServiceProvider services,
    IUserAccountService accounts,
    ISessionService sessions,
    IUserRepository users,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<TokenResponse> LoginAsync(LoginRequest request, Guid clientId, CancellationToken cancellationToken)
    {
        string username = UsernameRules.Normalise(request.Username);
        ExternalIdentity identity;
        try
        {
            identity = await provider.SignInAsync(username, request.Password, cancellationToken);
        }
        catch (ServiceException exception)
        {
            AuthLog.SignInRefused(logger, username, clientId, exception.Code);
            throw;
        }

        User user = await accounts.ResolveAsync(identity, provider.Kind, cancellationToken);
        return await sessions.StartAsync(user, clientId, cancellationToken);
    }

    public async Task<TokenResponse> ExchangeEntraAsync(EntraExchangeRequest request, Guid clientId, CancellationToken cancellationToken)
    {
        EntraTokenValidator validator = services.GetService<EntraTokenValidator>()
            ?? throw ServiceException.BadRequest(ErrorCodes.ModeMismatch, "This server does not sign in with Entra ID. Use POST /api/v1/auth/login.");

        ExternalIdentity identity = await validator.ValidateAsync(request.AccessToken, cancellationToken);
        User user = await accounts.ResolveAsync(identity, provider.Kind, cancellationToken);
        return await sessions.StartAsync(user, clientId, cancellationToken);
    }

    public async Task<CurrentUserResponse> MeAsync(Guid userId, CancellationToken cancellationToken)
    {
        User user = await users.FindAsync(userId, cancellationToken)
            ?? throw ServiceException.NotFound(ErrorCodes.UserNotFound, "The signed in user no longer exists.");
        return user.ToCurrentUser();
    }
}
