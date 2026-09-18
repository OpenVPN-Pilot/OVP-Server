using Microsoft.AspNetCore.Mvc;
using OpenVpnPilot.Server.Contracts.Responses;
using OpenVpnPilot.Server.Services.Sync;

namespace OpenVpnPilot.Server.Controllers;

/// <summary>Keeps a client's local copy of profiles, tags and vault entries in step.</summary>
[ApiController]
[Route("api/v1/sync")]
[Produces("application/json")]
public sealed class SyncController(ISyncService sync) : ControllerBase
{
    /// <summary>Everything that changed after a cursor, including deletions.</summary>
    /// <remarks>
    /// Start with <c>since=0</c> for the complete state and keep the returned cursor. 410 <c>sync.cursor_expired</c>
    /// means the delta can no longer be computed: start over from 0.
    /// </remarks>
    /// <param name="since">The cursor from the previous answer, or 0.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    [HttpGet("changes")]
    [ProducesResponseType<SyncChangesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status410Gone, "application/problem+json")]
    public Task<SyncChangesResponse> Changes([FromQuery] long since, CancellationToken cancellationToken) =>
        sync.ChangesAsync(since, cancellationToken);
}
