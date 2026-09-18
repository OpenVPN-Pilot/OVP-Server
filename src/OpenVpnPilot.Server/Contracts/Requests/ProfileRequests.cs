using System.ComponentModel.DataAnnotations;

namespace OpenVpnPilot.Server.Contracts.Requests;

/// <summary>A new profile. The configuration must be self contained: every certificate and key inline.</summary>
/// <param name="Name">The name shown in the client.</param>
/// <param name="Configuration">The complete OpenVPN configuration with inline blocks, as the client stores it after import. Never credentials.</param>
/// <param name="Notes">Free text shown in the client's detail panel.</param>
/// <param name="Colour">A colour as <c>#RRGGBB</c> or <c>#RRGGBBAA</c>.</param>
/// <param name="ProtectRoutes">Overrides the client's route protection for this profile; null follows the client setting.</param>
/// <param name="Tags">Tag names. Tags that do not exist yet are created.</param>
public sealed record ProfileCreateRequest(
    [Required, MaxLength(200)] string Name,
    [Required] string Configuration,
    [MaxLength(4000)] string? Notes,
    [RegularExpression(ContractPatterns.Colour)] string? Colour,
    bool? ProtectRoutes,
    [MaxLength(50)] IReadOnlyList<string>? Tags);

/// <summary>Replaces a profile's fields. Send the ETag of the version you read in <c>If-Match</c>.</summary>
/// <param name="Name">The name shown in the client.</param>
/// <param name="Configuration">A new configuration, or null to keep the current one.</param>
/// <param name="Notes">Free text; null clears it.</param>
/// <param name="Colour">A colour as <c>#RRGGBB</c> or <c>#RRGGBBAA</c>; null clears it.</param>
/// <param name="ProtectRoutes">Null follows the client setting.</param>
/// <param name="Tags">The complete list of tag names; null or empty removes every tag.</param>
public sealed record ProfileUpdateRequest(
    [Required, MaxLength(200)] string Name,
    string? Configuration,
    [MaxLength(4000)] string? Notes,
    [RegularExpression(ContractPatterns.Colour)] string? Colour,
    bool? ProtectRoutes,
    [MaxLength(50)] IReadOnlyList<string>? Tags);

/// <summary>Several new profiles at once, the server side of a bulk import.</summary>
/// <param name="Items">Up to 500 profiles. Each is accepted or refused on its own.</param>
public sealed record ProfileBatchRequest(
    [Required, MinLength(1), MaxLength(500)] IReadOnlyList<ProfileCreateRequest> Items);

/// <summary>A tag.</summary>
/// <param name="Name">Unique regardless of case.</param>
/// <param name="Colour">A colour as <c>#RRGGBB</c> or <c>#RRGGBBAA</c>.</param>
public sealed record TagRequest(
    [Required, MaxLength(100)] string Name,
    [RegularExpression(ContractPatterns.Colour)] string? Colour);

public static class ContractPatterns
{
    public const string Colour = "^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$";
}
