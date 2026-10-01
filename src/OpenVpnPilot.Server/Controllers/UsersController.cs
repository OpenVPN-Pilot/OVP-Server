using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenVpnPilot.Server.Auth;
using OpenVpnPilot.Server.Contracts.Responses;
using OpenVpnPilot.Server.Services.Users;

namespace OpenVpnPilot.Server.Controllers;

/// <summary>Who has signed in to this server, and switching them off. Administrators only.</summary>
[ApiController]
[Route("api/v1/users")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class UsersController(IUserAdminService users) : ControllerBase
{
    /// <summary>Every user who has signed in at least once.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<UserResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<UserResponse>> List(CancellationToken cancellationToken) => users.ListAsync(cancellationToken);

    /// <summary>One user.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public Task<UserResponse> Get(Guid id, CancellationToken cancellationToken) => users.GetAsync(id, cancellationToken);

    /// <summary>Disables a user. Their clients are told to erase everything from this server on their next request.</summary>
    [HttpPost("{id:guid}/disable")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    public Task<UserResponse> Disable(Guid id, CancellationToken cancellationToken) => users.DisableAsync(id, cancellationToken);

    /// <summary>Enables a disabled or deleted user again.</summary>
    [HttpPost("{id:guid}/enable")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    public Task<UserResponse> Enable(Guid id, CancellationToken cancellationToken) => users.EnableAsync(id, cancellationToken);

    /// <summary>Signs a user out on every installation without erasing anything.</summary>
    [HttpPost("{id:guid}/revoke-tokens")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    public Task<UserResponse> RevokeTokens(Guid id, CancellationToken cancellationToken) => users.RevokeTokensAsync(id, cancellationToken);

    /// <summary>Deletes a user. Their clients are told to erase everything from this server.</summary>
    /// <param name="id">The user.</param>
    /// <param name="purge">
    /// Also removes the record that the user existed, with their favourites, shortcuts and settings, and after it the
    /// same name may sign in again as new. Without the record the server no longer knows the account's tokens, so
    /// only a client that comes back within one access token lifetime is told to wipe itself. Delete without purge
    /// first, and purge once the clients have been told.
    /// </param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<NoContentResult> Delete(Guid id, [FromQuery] bool purge, CancellationToken cancellationToken)
    {
        await users.DeleteAsync(id, purge, cancellationToken);
        return NoContent();
    }
}
