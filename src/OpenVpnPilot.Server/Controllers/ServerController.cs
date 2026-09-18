using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenVpnPilot.Server.Contracts.Responses;
using OpenVpnPilot.Server.Services;

namespace OpenVpnPilot.Server.Controllers;

/// <summary>What a client asks first, before it knows how to sign in.</summary>
[ApiController]
[Route("api/v1/server")]
[Produces("application/json")]
public sealed class ServerController(IServerInfoService info) : ControllerBase
{
    /// <summary>Describes this server: its version, the minimum client version and how to sign in.</summary>
    /// <remarks>Anonymous, and the only API endpoint that needs none of the <c>X-Pilot-*</c> headers.</remarks>
    [HttpGet("info")]
    [AllowAnonymous]
    [ProducesResponseType<ServerInfoResponse>(StatusCodes.Status200OK)]
    public ServerInfoResponse Info() => info.Describe();
}
