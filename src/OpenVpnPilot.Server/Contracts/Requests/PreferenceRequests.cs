using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace OpenVpnPilot.Server.Contracts.Requests;

/// <summary>The complete list of the caller's favourites. Replaces whatever was stored.</summary>
/// <param name="Items">Favourite profiles, each optionally in a numbered slot.</param>
public sealed record FavouritesRequest(
    [Required, MaxLength(1000), NoNullItems] IReadOnlyList<FavouriteItem> Items);

/// <summary>A favourite profile.</summary>
/// <param name="ProfileId">The profile.</param>
/// <param name="Slot">1 to 10, the slot bound to the favourite shortcuts (10 is the zero key); null for a favourite without a slot. Each slot at most once.</param>
public sealed record FavouriteItem(
    Guid ProfileId,
    [Range(1, 10)] int? Slot);

/// <summary>The complete list of the caller's shortcuts. Replaces whatever was stored.</summary>
/// <param name="Items">One entry per action.</param>
public sealed record HotkeysRequest(
    [Required, MaxLength(200), NoNullItems] IReadOnlyList<HotkeyItem> Items);

/// <summary>A shortcut, as the client's own shortcut settings hold it.</summary>
/// <param name="ActionId">The client's action identifier, for example <c>ToggleQuickSwitcher</c> or <c>ConnectFavourite1</c>. Each at most once.</param>
/// <param name="Gesture">The key combination as the client writes it, for example <c>Control+Alt+V</c>.</param>
/// <param name="ProfileId">A profile the action applies to, for actions that take one.</param>
/// <param name="IsEnabled">Whether the shortcut is active.</param>
public sealed record HotkeyItem(
    [Required, MaxLength(100)] string ActionId,
    [Required, MaxLength(100)] string Gesture,
    Guid? ProfileId,
    bool IsEnabled);

/// <summary>The caller's portable settings, stored as the client sends them.</summary>
/// <param name="SchemaVersion">The client's settings schema version, so a newer client can migrate what an older one stored.</param>
/// <param name="Document">A JSON object of at most 64 KiB. The server keeps it and does not interpret it.</param>
public sealed record SettingsRequest(
    [Range(0, int.MaxValue)] int SchemaVersion,
    JsonElement Document);
