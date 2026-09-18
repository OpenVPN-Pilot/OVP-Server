using System.Text.Json;
using OpenVpnPilot.Server.Contracts.Requests;

namespace OpenVpnPilot.Server.Contracts.Responses;

/// <summary>The caller's favourites, slotted ones first in slot order.</summary>
/// <param name="Items">Favourite profiles.</param>
public sealed record FavouritesResponse(IReadOnlyList<FavouriteItem> Items);

/// <summary>The caller's shortcuts.</summary>
/// <param name="Items">One entry per action.</param>
public sealed record HotkeysResponse(IReadOnlyList<HotkeyItem> Items);

/// <summary>The caller's stored settings.</summary>
/// <param name="SchemaVersion">The schema version the document was written with; 0 when nothing is stored yet.</param>
/// <param name="Document">The stored JSON object; an empty object when nothing is stored yet.</param>
/// <param name="ETag">Send in <c>If-Match</c> to replace exactly this version; null when nothing is stored yet.</param>
/// <param name="UpdatedAt">When it was last stored.</param>
public sealed record SettingsResponse(
    int SchemaVersion,
    JsonElement Document,
    string? ETag,
    DateTimeOffset? UpdatedAt);
