using Microsoft.EntityFrameworkCore;
using OpenVpnPilot.Server.Data;
using OpenVpnPilot.Server.Data.Entities;
using OpenVpnPilot.Server.Repositories.Interfaces;

namespace OpenVpnPilot.Server.Repositories;

public sealed class TagRepository(PilotServerDbContext db) : ITagRepository
{
    public async Task<IReadOnlyList<Tag>> ListAsync(CancellationToken cancellationToken) =>
        await db.Tags.AsNoTracking().OrderBy(t => t.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Tag>> ChangedSinceAsync(long changeSeq, CancellationToken cancellationToken) =>
        await db.Tags.AsNoTracking().Where(t => t.ChangeSeq > changeSeq).OrderBy(t => t.ChangeSeq).ToListAsync(cancellationToken);

    public Task<Tag?> FindAsync(Guid id, bool withProfiles, CancellationToken cancellationToken)
    {
        IQueryable<Tag> query = db.Tags;
        if (withProfiles)
        {
            query = query.Include(t => t.Profiles);
        }

        return query.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Tag>> FindByNamesAsync(IReadOnlyCollection<string> names, CancellationToken cancellationToken)
    {
        // citext makes Contains compare without regard to case, as the unique index does.
        List<string> wanted = [.. names];
        return await db.Tags.Where(t => wanted.Contains(t.Name)).ToListAsync(cancellationToken);
    }

    public Task<bool> NameTakenAsync(string name, Guid? except, CancellationToken cancellationToken) =>
        db.Tags.AnyAsync(t => t.Name == name && t.Id != except, cancellationToken);

    public void Add(Tag tag) => db.Tags.Add(tag);

    public void Remove(Tag tag) => db.Tags.Remove(tag);
}
