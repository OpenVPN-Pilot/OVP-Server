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

// An If-Match header as RFC 9110 defines it: "*" for any current version, or a list of entity tags.
// A tag this server could not have issued is kept as one that matches nothing, so a malformed header
// fails the precondition instead of being mistaken for an absent one.
public sealed record IfMatch(bool Any, IReadOnlyList<uint> Versions)
{
    public bool Matches(uint version) => Any || Versions.Contains(version);
}

public static class ETags
{
    public static string Format(uint version) => "\"" + version.ToString(CultureInfo.InvariantCulture) + "\"";

    // Null only when the header is absent or empty.
    public static IfMatch? Parse(string? header)
    {
        string? value = header?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (value == "*")
        {
            return new IfMatch(true, []);
        }

        List<uint> versions = [];
        foreach (string part in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            string tag = part.StartsWith("W/", StringComparison.Ordinal) ? part[2..] : part;
            if (tag.Length > 2 && tag[0] == '"' && tag[^1] == '"'
                && uint.TryParse(tag[1..^1], NumberStyles.None, CultureInfo.InvariantCulture, out uint version))
            {
                versions.Add(version);
            }
        }

        return new IfMatch(false, versions);
    }
}
