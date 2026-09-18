using OpenVpnPilot.Server.Auth;
using OpenVpnPilot.Server.Contracts;
using OpenVpnPilot.Server.Contracts.Requests;
using OpenVpnPilot.Server.Contracts.Responses;
using OpenVpnPilot.Server.Data.Entities;
using OpenVpnPilot.Server.Mapping;
using OpenVpnPilot.Server.Repositories.Interfaces;

namespace OpenVpnPilot.Server.Services.Profiles;

public interface ITagService
{
    public Task<IReadOnlyList<TagResponse>> ListAsync(CancellationToken cancellationToken);

    public Task<TagResponse> CreateAsync(TagRequest request, CancellationToken cancellationToken);

    public Task<TagResponse> UpdateAsync(Guid id, TagRequest request, CancellationToken cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class TagService(
    ITagRepository tags,
    ISyncRepository sync,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider time,
    ILogger<TagService> logger) : ITagService
{
    public async Task<IReadOnlyList<TagResponse>> ListAsync(CancellationToken cancellationToken) =>
        [.. (await tags.ListAsync(cancellationToken)).Select(t => t.ToResponse())];

    public async Task<TagResponse> CreateAsync(TagRequest request, CancellationToken cancellationToken)
    {
        string name = Name(request);
        await using ISyncedWrite write = await unitOfWork.BeginSyncedWriteAsync(cancellationToken);
        await RefuseTakenAsync(name, null, cancellationToken);

        DateTimeOffset now = time.GetUtcNow();
        Tag tag = new()
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Colour = request.Colour,
            ChangeSeq = write.ChangeSeq,
            CreatedAt = now,
            UpdatedAt = now,
        };
        tags.Add(tag);
        await write.CommitAsync(cancellationToken);
        ProfileLog.TagCreated(logger, currentUser.Username, name, write.ChangeSeq);
        return tag.ToResponse();
    }

    public async Task<TagResponse> UpdateAsync(Guid id, TagRequest request, CancellationToken cancellationToken)
    {
        string name = Name(request);
        await using ISyncedWrite write = await unitOfWork.BeginSyncedWriteAsync(cancellationToken);
        Tag tag = await FindAsync(id, cancellationToken);
        await RefuseTakenAsync(name, id, cancellationToken);

        // A profile lists its tags by name, so a rename changes every profile carrying the tag.
        if (!string.Equals(tag.Name, name, StringComparison.Ordinal))
        {
            BumpProfiles(tag, write.ChangeSeq);
        }

        tag.Name = name;
        tag.Colour = request.Colour;
        tag.ChangeSeq = write.ChangeSeq;
        tag.UpdatedAt = time.GetUtcNow();
        await write.CommitAsync(cancellationToken);
        ProfileLog.TagUpdated(logger, currentUser.Username, name, write.ChangeSeq);
        return tag.ToResponse();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using ISyncedWrite write = await unitOfWork.BeginSyncedWriteAsync(cancellationToken);
        Tag tag = await FindAsync(id, cancellationToken);
        int carrying = tag.Profiles.Count;
        BumpProfiles(tag, write.ChangeSeq);
        tags.Remove(tag);
        sync.AddTombstone(new Tombstone
        {
            Kind = TombstoneKind.Tag,
            EntityId = tag.Id,
            ChangeSeq = write.ChangeSeq,
            DeletedAt = time.GetUtcNow(),
        });

        await write.CommitAsync(cancellationToken);
        ProfileLog.TagDeleted(logger, currentUser.Username, tag.Name, carrying, write.ChangeSeq);
    }

    private void BumpProfiles(Tag tag, long changeSeq)
    {
        DateTimeOffset now = time.GetUtcNow();
        foreach (Profile profile in tag.Profiles)
        {
            profile.ChangeSeq = changeSeq;
            profile.UpdatedAt = now;
            profile.UpdatedBy = currentUser.Username;
        }
    }

    private async Task<Tag> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await tags.FindAsync(id, withProfiles: true, cancellationToken)
            ?? throw ServiceException.NotFound(ErrorCodes.TagNotFound, $"There is no tag {id}.");

    private async Task RefuseTakenAsync(string name, Guid? except, CancellationToken cancellationToken)
    {
        if (await tags.NameTakenAsync(name, except, cancellationToken))
        {
            throw ServiceException.Conflict(ErrorCodes.TagDuplicate, $"A tag named '{name}' already exists.");
        }
    }

    private static string Name(TagRequest request)
    {
        List<string> names = TagAssigner.Normalise([request.Name]);
        return names.Count == 1 ? names[0] : throw ServiceException.Invalid("name", "A tag needs a name.");
    }
}
