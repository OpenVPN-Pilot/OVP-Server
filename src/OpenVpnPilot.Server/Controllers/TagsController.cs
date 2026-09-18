using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenVpnPilot.Server.Auth;
using OpenVpnPilot.Server.Contracts.Requests;
using OpenVpnPilot.Server.Contracts.Responses;
using OpenVpnPilot.Server.Services.Profiles;

namespace OpenVpnPilot.Server.Controllers;

/// <summary>The team's shared tags.</summary>
[ApiController]
[Route("api/v1/tags")]
[Produces("application/json")]
public sealed class TagsController(ITagService tags) : ControllerBase
{
    /// <summary>Lists every tag.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TagResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<TagResponse>> List(CancellationToken cancellationToken) => tags.ListAsync(cancellationToken);

    /// <summary>Creates a tag. Administrators only.</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType<TagResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<CreatedResult> Create(TagRequest request, CancellationToken cancellationToken)
    {
        TagResponse created = await tags.CreateAsync(request, cancellationToken);
        return Created($"/api/v1/tags/{created.Id}", created);
    }

    /// <summary>Renames or recolours a tag. Every profile carrying it counts as changed. Administrators only.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType<TagResponse>(StatusCodes.Status200OK)]
    public Task<TagResponse> Update(Guid id, TagRequest request, CancellationToken cancellationToken) =>
        tags.UpdateAsync(id, request, cancellationToken);

    /// <summary>Deletes a tag and removes it from every profile. Administrators only.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<NoContentResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await tags.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
