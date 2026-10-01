using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace OpenVpnPilot.Server.Contracts.Requests;

/// <summary>A new profile. The configuration must be self contained: every certificate and key inline.</summary>
/// <param name="Name">The name shown in the client.</param>
/// <param name="Configuration">The complete OpenVPN configuration with inline blocks, as the client stores it after import. Never credentials.</param>
/// <param name="Notes">Free text shown in the client's detail panel.</param>
/// <param name="Colour">A colour as <c>#RRGGBB</c> or <c>#RRGGBBAA</c>.</param>
/// <param name="ProtectRoutes">Overrides the client's route protection for this profile; null follows the client setting.</param>
/// <param name="Tags">Tag names. Tags that do not exist yet are created.</param>
public sealed record ProfileCreateRequest(
    [Required, MaxLength(ProfileLimits.Name)] string Name,
    [Required] string Configuration,
    [MaxLength(ProfileLimits.Notes)] string? Notes,
    [RegularExpression(ContractPatterns.Colour)] string? Colour,
    bool? ProtectRoutes,
    [MaxLength(ProfileLimits.Tags), NoNullItems] IReadOnlyList<string>? Tags);

/// <summary>Replaces a profile's fields. Send the ETag of the version you read in <c>If-Match</c>.</summary>
/// <param name="Name">The name shown in the client.</param>
/// <param name="Configuration">A new configuration, or null to keep the current one.</param>
/// <param name="Notes">Free text; null clears it.</param>
/// <param name="Colour">A colour as <c>#RRGGBB</c> or <c>#RRGGBBAA</c>; null clears it.</param>
/// <param name="ProtectRoutes">Null follows the client setting.</param>
/// <param name="Tags">The complete list of tag names; null or empty removes every tag.</param>
public sealed record ProfileUpdateRequest(
    [Required, MaxLength(ProfileLimits.Name)] string Name,
    string? Configuration,
    [MaxLength(ProfileLimits.Notes)] string? Notes,
    [RegularExpression(ContractPatterns.Colour)] string? Colour,
    bool? ProtectRoutes,
    [MaxLength(ProfileLimits.Tags), NoNullItems] IReadOnlyList<string>? Tags);

/// <summary>Several new profiles at once, the server side of a bulk import.</summary>
/// <param name="Items">
/// 1 to 500 profiles. Each is accepted or refused on its own, including for the limits of its fields, so one
/// bad item never fails the others.
/// </param>
// The items are deliberately not validated by MVC: that would refuse the whole batch over one of them.
public sealed record ProfileBatchRequest(
    [Required, MinLength(1), MaxLength(ProfileLimits.BatchItems), ValidateNever] IReadOnlyList<ProfileCreateRequest?> Items);

/// <summary>A tag.</summary>
/// <param name="Name">Unique regardless of case.</param>
/// <param name="Colour">A colour as <c>#RRGGBB</c> or <c>#RRGGBBAA</c>.</param>
public sealed record TagRequest(
    [Required, MaxLength(ProfileLimits.TagName)] string Name,
    [RegularExpression(ContractPatterns.Colour)] string? Colour);

public static class ContractPatterns
{
    public const string Colour = "^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$";
}

// The limits a profile request is held to, in one place, because the batch checks them itself.
public static class ProfileLimits
{
    public const int Name = 200;
    public const int Notes = 4000;
    public const int Tags = 50;
    public const int TagName = 100;
    public const int BatchItems = 500;

    // The batch body may hold 500 profiles of a typical size; Kestrel's default of 30 MB would refuse
    // far less, and without the problem shape every other refusal has.
    public const long BatchBodyBytes = 64L * 1024 * 1024;
}
