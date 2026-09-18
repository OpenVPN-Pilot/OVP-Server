using Microsoft.EntityFrameworkCore;
using OpenVpnPilot.Server.Data;
using OpenVpnPilot.Server.Data.Entities;
using OpenVpnPilot.Server.Repositories.Interfaces;

namespace OpenVpnPilot.Server.Repositories;

public sealed class ProfileRepository(PilotServerDbContext db) : IProfileRepository
{
    public async Task<IReadOnlyList<Profile>> ListAsync(string? tag, string? search, CancellationToken cancellationToken)
    {
        IQueryable<Profile> query = db.Profiles.AsNoTracking().Include(p => p.Tags);

        if (tag is not null)
        {
            query = query.Where(p => p.Tags.Any(t => t.Name == tag));
        }

        if (search is not null)
        {
            // Searches what the client's own search covers: name, remote host and tag.
            string pattern = "%" + EscapeLike(search) + "%";
            query = query.Where(p =>
                EF.Functions.ILike(p.Name, pattern, "\\")
                || (p.RemoteHost != null && EF.Functions.ILike(p.RemoteHost, pattern, "\\"))
                || p.Tags.Any(t => EF.Functions.ILike(t.Name, pattern, "\\")));
        }

        return await query.OrderBy(p => p.Name).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Profile>> ChangedSinceAsync(long changeSeq, CancellationToken cancellationToken) =>
        await db.Profiles
            .AsNoTracking()
            .Include(p => p.Tags)
            .Where(p => p.ChangeSeq > changeSeq)
            .OrderBy(p => p.ChangeSeq)
            .ToListAsync(cancellationToken);

    public Task<Profile?> FindAsync(Guid id, bool withContent, CancellationToken cancellationToken)
    {
        IQueryable<Profile> query = db.Profiles.Include(p => p.Tags);
        if (withContent)
        {
            query = query.Include(p => p.Content);
        }

        return query.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public Task<string?> FindNameByHashAsync(string contentHash, Guid? except, CancellationToken cancellationToken) =>
        db.Profiles
            .AsNoTracking()
            .Where(p => p.ContentHash == contentHash && p.Id != except)
            .Select(p => p.Name)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlySet<Guid>> ExistingIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        List<Guid> wanted = [.. ids.Distinct()];
        List<Guid> found = await db.Profiles.Where(p => wanted.Contains(p.Id)).Select(p => p.Id).ToListAsync(cancellationToken);
        return found.ToHashSet();
    }

    public void Add(Profile profile) => db.Profiles.Add(profile);

    public void Remove(Profile profile) => db.Profiles.Remove(profile);

    public void ExpectVersion(Profile profile, uint version) =>
        db.Entry(profile).Property(p => p.Version).OriginalValue = version;

    private static string EscapeLike(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
