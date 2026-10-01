using OpenVpnPilot.Server.Data.Entities;
using OpenVpnPilot.Server.Repositories.Interfaces;

namespace OpenVpnPilot.Server.Services.Profiles;

public interface ITagAssigner
{
    public Task<List<Tag>> ResolveAsync(IReadOnlyList<string?>? names, long changeSeq, CancellationToken cancellationToken);
}

// Turns tag names into tags, creating the ones that do not exist. Scoped to one request, so a batch
// that names the same new tag twenty times creates it once.
public sealed class TagAssigner(ITagRepository tags, TimeProvider time) : ITagAssigner
{
    public const int MaximumNameLength = Contracts.Requests.ProfileLimits.TagName;

    private readonly Dictionary<string, Tag> known = new(StringComparer.OrdinalIgnoreCase);

    public async Task<List<Tag>> ResolveAsync(IReadOnlyList<string?>? names, long changeSeq, CancellationToken cancellationToken)
    {
        List<string> wanted = Normalise(names);
        List<string> unknown = [.. wanted.Where(n => !known.ContainsKey(n))];
        if (unknown.Count > 0)
        {
            foreach (Tag existing in await tags.FindByNamesAsync(unknown, cancellationToken))
            {
                known[existing.Name] = existing;
            }
        }

        DateTimeOffset now = time.GetUtcNow();
        List<Tag> result = [];
        foreach (string name in wanted)
        {
            if (!known.TryGetValue(name, out Tag? tag))
            {
                tag = new Tag { Id = Guid.CreateVersion7(), Name = name, ChangeSeq = changeSeq, CreatedAt = now, UpdatedAt = now };
                tags.Add(tag);
                known[name] = tag;
            }

            result.Add(tag);
        }

        return result;
    }

    public static List<string> Normalise(IReadOnlyList<string?>? names)
    {
        if (names?.Count > Contracts.Requests.ProfileLimits.Tags)
        {
            throw ServiceException.Invalid("tags", $"A profile carries at most {Contracts.Requests.ProfileLimits.Tags} tags.");
        }

        List<string> result = [];
        foreach (string? raw in names ?? [])
        {
            string name = raw?.Trim() ?? throw ServiceException.Invalid("tags", "A tag name cannot be null.");
            if (name.Length == 0)
            {
                continue;
            }

            if (name.Length > MaximumNameLength)
            {
                throw ServiceException.Invalid("tags", $"A tag name is at most {MaximumNameLength} characters: '{name[..20]}...'.");
            }

            if (name.Any(char.IsControl))
            {
                throw ServiceException.Invalid("tags", "A tag name cannot contain control characters.");
            }

            if (!result.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(name);
            }
        }

        return result;
    }
}
