using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenVpnPilot.Server.Auth;
using OpenVpnPilot.Server.Contracts.Requests;
using OpenVpnPilot.Server.Contracts.Responses;
using OpenVpnPilot.Server.Services.Vault;

namespace OpenVpnPilot.Server.Controllers;

/// <summary>
/// The shared vault: one sign in per profile and realm, so nobody types the same password a second time.
/// Everyone reads and adds; administrators replace and delete.
/// </summary>
[ApiController]
[Produces("application/json")]
public sealed class VaultController(IVaultService vault) : ControllerBase
{
    /// <summary>Every vault entry of every profile, with its secret. Meant for the first synchronisation.</summary>
    [HttpGet("api/v1/vault")]
    [ProducesResponseType<IReadOnlyList<VaultEntryResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<VaultEntryResponse>> ListAll(CancellationToken cancellationToken) =>
        vault.ListAsync(null, cancellationToken);

    /// <summary>The vault entries of one profile.</summary>
    [HttpGet("api/v1/profiles/{profileId:guid}/vault")]
    [ProducesResponseType<IReadOnlyList<VaultEntryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public Task<IReadOnlyList<VaultEntryResponse>> ListForProfile(Guid profileId, CancellationToken cancellationToken) =>
        vault.ListAsync(profileId, cancellationToken);

    /// <summary>Adds a sign in the vault does not hold yet. Open to every user.</summary>
    /// <remarks>Answers 409 <c>vault.entry_exists</c> when one is already stored; only an administrator can replace it.</remarks>
    /// <param name="profileId">The profile.</param>
    /// <param name="realm">OpenVPN's realm, URL encoded: <c>Auth</c>, or the private key name for a passphrase.</param>
    /// <param name="request">The sign in.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    [HttpPost("api/v1/profiles/{profileId:guid}/vault/{realm}")]
    [ProducesResponseType<VaultEntryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<CreatedResult> Add(Guid profileId, string realm, VaultEntryRequest request, CancellationToken cancellationToken)
    {
        VaultEntryResponse created = await vault.AddAsync(profileId, realm, request, cancellationToken);
        return Created($"/api/v1/profiles/{profileId}/vault/{Uri.EscapeDataString(created.Realm)}", created);
    }

    /// <summary>Stores a sign in, replacing any that exists. Administrators only.</summary>
    [HttpPut("api/v1/profiles/{profileId:guid}/vault/{realm}")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType<VaultEntryResponse>(StatusCodes.Status200OK)]
    public Task<VaultEntryResponse> Replace(Guid profileId, string realm, VaultEntryRequest request, CancellationToken cancellationToken) =>
        vault.ReplaceAsync(profileId, realm, request, cancellationToken);

    /// <summary>Deletes a sign in. Administrators only.</summary>
    [HttpDelete("api/v1/profiles/{profileId:guid}/vault/{realm}")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<NoContentResult> Delete(Guid profileId, string realm, CancellationToken cancellationToken)
    {
        await vault.DeleteAsync(profileId, realm, cancellationToken);
        return NoContent();
    }
}
