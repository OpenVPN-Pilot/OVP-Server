using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenVpnPilot.Server.Auth;
using OpenVpnPilot.Server.Contracts.Requests;
using OpenVpnPilot.Server.Contracts.Responses;
using OpenVpnPilot.Server.Mapping;
using OpenVpnPilot.Server.Services.Profiles;

namespace OpenVpnPilot.Server.Controllers;

/// <summary>The team's shared profiles. Everyone reads them; administrators change them.</summary>
[ApiController]
[Route("api/v1/profiles")]
[Produces("application/json")]
public sealed class ProfilesController(IProfileService profiles, IProfileImportService imports) : ControllerBase
{
    /// <summary>Lists profiles without their configurations.</summary>
    /// <param name="tag">Only profiles carrying this tag.</param>
    /// <param name="search">Matches name, remote host and tag, regardless of case.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProfileResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ProfileResponse>> List([FromQuery] string? tag, [FromQuery] string? search, CancellationToken cancellationToken) =>
        profiles.ListAsync(tag, search, cancellationToken);

    /// <summary>One profile without its configuration.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ProfileResponse> Get(Guid id, CancellationToken cancellationToken) =>
        WithETag(await profiles.GetAsync(id, cancellationToken));

    /// <summary>The configuration of a profile, with every certificate and key inline: what the client connects with.</summary>
    [HttpGet("{id:guid}/configuration")]
    [ProducesResponseType<ProfileConfigurationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public Task<ProfileConfigurationResponse> Configuration(Guid id, CancellationToken cancellationToken) =>
        profiles.GetConfigurationAsync(id, cancellationToken);

    /// <summary>Creates a profile. Administrators only.</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<CreatedAtActionResult> Create(ProfileCreateRequest request, CancellationToken cancellationToken)
    {
        ProfileResponse created = WithETag(await imports.CreateAsync(request, cancellationToken));
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Creates up to 500 profiles at once. Each item is accepted or refused on its own. Administrators only.</summary>
    [HttpPost("batch")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [RequestSizeLimit(ProfileLimits.BatchBodyBytes)]
    [ProducesResponseType<ProfileBatchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, "application/problem+json")]
    public Task<ProfileBatchResponse> CreateBatch(ProfileBatchRequest request, CancellationToken cancellationToken) =>
        imports.CreateBatchAsync(request, cancellationToken);

    /// <summary>Replaces a profile's fields. Requires <c>If-Match</c> with the ETag last read. Administrators only.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, "application/problem+json")]
    public async Task<ProfileResponse> Update(
        Guid id, ProfileUpdateRequest request, [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken cancellationToken) =>
        WithETag(await profiles.UpdateAsync(id, request, ETags.Parse(ifMatch), cancellationToken));

    /// <summary>Deletes a profile together with its vault entries. Administrators only.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<NoContentResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await profiles.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    private ProfileResponse WithETag(ProfileResponse profile)
    {
        Response.Headers.ETag = profile.ETag;
        return profile;
    }
}
