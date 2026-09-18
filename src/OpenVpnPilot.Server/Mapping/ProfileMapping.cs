using System.Globalization;
using OpenVpnPilot.Server.Contracts.Responses;
using OpenVpnPilot.Server.Data.Entities;

namespace OpenVpnPilot.Server.Mapping;

public static class ProfileMapping
{
    public static ProfileResponse ToResponse(this Profile profile) =>
        new(
            profile.Id,
            profile.Name,
            profile.RemoteHost,
            profile.RemotePort,
            profile.Protocol,
            profile.RequiresCredentials,
            profile.HasUnsupportedOptions,
            profile.ProtectRoutes,
            profile.Notes,
            profile.Colour,
            [.. profile.Tags.Select(t => t.Name).Order(StringComparer.OrdinalIgnoreCase)],
            profile.ContentHash,
            profile.ChangeSeq,
            ETags.Format(profile.Version),
            profile.CreatedAt,
            profile.CreatedBy,
            profile.UpdatedAt,
            profile.UpdatedBy);

    public static TagResponse ToResponse(this Tag tag) => new(tag.Id, tag.Name, tag.Colour, tag.ChangeSeq);
}

public static class ETags
{
    public static string Format(uint version) => "\"" + version.ToString(CultureInfo.InvariantCulture) + "\"";

    // Accepts the value as sent in If-Match, quoted and optionally weak.
    public static uint? Parse(string? header)
    {
        string? value = header?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (value.StartsWith("W/", StringComparison.Ordinal))
        {
            value = value[2..];
        }

        return uint.TryParse(value.Trim('"'), NumberStyles.None, CultureInfo.InvariantCulture, out uint version) ? version : null;
    }
}
