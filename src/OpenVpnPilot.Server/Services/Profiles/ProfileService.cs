using OpenVpnPilot.Server.Auth;
using OpenVpnPilot.Server.Contracts;
using OpenVpnPilot.Server.Contracts.Requests;
using OpenVpnPilot.Server.Contracts.Responses;
using OpenVpnPilot.Server.Data.Entities;
using OpenVpnPilot.Server.Mapping;
using OpenVpnPilot.Server.Repositories.Interfaces;
using OpenVpnPilot.Server.Security;

namespace OpenVpnPilot.Server.Services.Profiles;

public interface IProfileService
{
    public Task<IReadOnlyList<ProfileResponse>> ListAsync(string? tag, string? search, CancellationToken cancellationToken);

    public Task<ProfileResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    public Task<ProfileConfigurationResponse> GetConfigurationAsync(Guid id, CancellationToken cancellationToken);

    public Task<ProfileResponse> UpdateAsync(Guid id, ProfileUpdateRequest request, IfMatch? precondition, CancellationToken cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class ProfileService(
    IProfileRepository profiles,
    ISyncRepository sync,
    IUnitOfWork unitOfWork,
    IProfileFactory factory,
    ITagAssigner tagAssigner,
    ICurrentUser currentUser,
    TimeProvider time,
    ILogger<ProfileService> logger) : IProfileService
{
    public async Task<IReadOnlyList<ProfileResponse>> ListAsync(string? tag, string? search, CancellationToken cancellationToken)
    {
        IReadOnlyList<Profile> found = await profiles.ListAsync(Blank(tag), Blank(search), cancellationToken);
        ProfileLog.Listed(logger, currentUser.Username, found.Count);
        return [.. found.Select(p => p.ToResponse())];
    }

    public async Task<ProfileResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        (await FindAsync(id, withContent: false, cancellationToken)).ToResponse();

    public async Task<ProfileConfigurationResponse> GetConfigurationAsync(Guid id, CancellationToken cancellationToken)
    {
        Profile profile = await FindAsync(id, withContent: true, cancellationToken);
        string configuration = factory.ReadConfiguration(profile);
        ProfileLog.ConfigurationRead(logger, currentUser.Username, profile.Name, profile.Id);
        return new ProfileConfigurationResponse(profile.Id, profile.ContentHash, configuration);
    }

    public async Task<ProfileResponse> UpdateAsync(
        Guid id, ProfileUpdateRequest request, IfMatch? precondition, CancellationToken cancellationToken)
    {
        if (precondition is null)
        {
            throw new ServiceException(StatusCodes.Status428PreconditionRequired, ErrorCodes.PreconditionRequired,
                "Send the ETag of the profile you read in If-Match, so a change made meanwhile is not overwritten.");
        }

        ProfileFactory.RequireName(request.Name);
        OvpnInspection? inspection = request.Configuration is null ? null : OvpnInspector.Inspect(request.Configuration);
        string? hash = inspection is null ? null : TokenHashing.ContentHash(inspection.Configuration);

        await using ISyncedWrite write = await unitOfWork.BeginSyncedWriteAsync(cancellationToken);
        Profile profile = await FindAsync(id, withContent: request.Configuration is not null, cancellationToken);
        if (!precondition.Matches(profile.Version))
        {
            throw ServiceException.PreconditionFailed("The profile was changed since it was read. Read it again and retry.");
        }

        // Also catches a change committed between reading the profile here and saving it.
        profiles.ExpectVersion(profile, profile.Version);

        bool configurationChanged = hash is not null && hash != profile.ContentHash;
        if (configurationChanged)
        {
            await RefuseDuplicateAsync(hash!, profile.Id, cancellationToken);
            factory.ApplyConfiguration(profile, inspection!, hash!);
        }

        profile.Name = request.Name.Trim();
        profile.Notes = request.Notes;
        profile.Colour = request.Colour;
        profile.ProtectRoutes = request.ProtectRoutes;
        List<Tag> tags = await tagAssigner.ResolveAsync(request.Tags, write.ChangeSeq, cancellationToken);
        profile.Tags.Clear();
        profile.Tags.AddRange(tags);
        factory.Touch(profile, write.ChangeSeq);

        await write.CommitAsync(cancellationToken);
        ProfileLog.Updated(logger, currentUser.Username, profile.Name, profile.Id, configurationChanged, write.ChangeSeq);
        return profile.ToResponse();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using ISyncedWrite write = await unitOfWork.BeginSyncedWriteAsync(cancellationToken);
        Profile profile = await FindAsync(id, withContent: false, cancellationToken);
        profiles.Remove(profile);

        // The profile's vault entries go with it through the cascade; a client drops them with the profile.
        sync.AddTombstone(new Tombstone
        {
            Kind = TombstoneKind.Profile,
            EntityId = profile.Id,
            ChangeSeq = write.ChangeSeq,
            DeletedAt = time.GetUtcNow(),
        });

        await write.CommitAsync(cancellationToken);
        ProfileLog.Deleted(logger, currentUser.Username, profile.Name, profile.Id, write.ChangeSeq);
    }

    private async Task<Profile> FindAsync(Guid id, bool withContent, CancellationToken cancellationToken) =>
        await profiles.FindAsync(id, withContent, cancellationToken)
            ?? throw ServiceException.NotFound(ErrorCodes.ProfileNotFound, $"There is no profile {id}.");

    private async Task RefuseDuplicateAsync(string hash, Guid? except, CancellationToken cancellationToken)
    {
        string? existing = await profiles.FindNameByHashAsync(hash, except, cancellationToken);
        if (existing is not null)
        {
            throw ServiceException.Conflict(ErrorCodes.ProfileDuplicate, $"The profile '{existing}' has exactly this configuration.");
        }
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
