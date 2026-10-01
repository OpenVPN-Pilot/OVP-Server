using Microsoft.AspNetCore.Mvc;
using OpenVpnPilot.Server.Contracts.Requests;
using OpenVpnPilot.Server.Contracts.Responses;
using OpenVpnPilot.Server.Mapping;
using OpenVpnPilot.Server.Services.Preferences;

namespace OpenVpnPilot.Server.Controllers;

/// <summary>The caller's own favourites, shortcuts and settings, which follow them to every machine.</summary>
[ApiController]
[Route("api/v1/me")]
[Produces("application/json")]
public sealed class MeController(IPreferenceService preferences, ISettingsService settings) : ControllerBase
{
    /// <summary>The caller's favourites.</summary>
    [HttpGet("favourites")]
    [ProducesResponseType<FavouritesResponse>(StatusCodes.Status200OK)]
    public Task<FavouritesResponse> Favourites(CancellationToken cancellationToken) =>
        preferences.FavouritesAsync(cancellationToken);

    /// <summary>Replaces the caller's favourites with this list.</summary>
    [HttpPut("favourites")]
    [ProducesResponseType<FavouritesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public Task<FavouritesResponse> ReplaceFavourites(FavouritesRequest request, CancellationToken cancellationToken) =>
        preferences.ReplaceFavouritesAsync(request, cancellationToken);

    /// <summary>The caller's shortcuts.</summary>
    [HttpGet("hotkeys")]
    [ProducesResponseType<HotkeysResponse>(StatusCodes.Status200OK)]
    public Task<HotkeysResponse> Hotkeys(CancellationToken cancellationToken) =>
        preferences.HotkeysAsync(cancellationToken);

    /// <summary>Replaces the caller's shortcuts with this list.</summary>
    [HttpPut("hotkeys")]
    [ProducesResponseType<HotkeysResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public Task<HotkeysResponse> ReplaceHotkeys(HotkeysRequest request, CancellationToken cancellationToken) =>
        preferences.ReplaceHotkeysAsync(request, cancellationToken);

    /// <summary>The caller's settings document.</summary>
    [HttpGet("settings")]
    [ProducesResponseType<SettingsResponse>(StatusCodes.Status200OK)]
    public Task<SettingsResponse> Settings(CancellationToken cancellationToken) =>
        settings.GetAsync(cancellationToken);

    /// <summary>Stores the caller's settings document. <c>If-Match</c> is optional and, when sent, must match.</summary>
    [HttpPut("settings")]
    [ProducesResponseType<SettingsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    public Task<SettingsResponse> ReplaceSettings(
        SettingsRequest request, [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken cancellationToken) =>
        settings.ReplaceAsync(request, ETags.Parse(ifMatch), cancellationToken);
}
