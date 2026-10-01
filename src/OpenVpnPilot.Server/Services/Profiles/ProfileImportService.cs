using OpenVpnPilot.Server.Auth;
using OpenVpnPilot.Server.Contracts;
using OpenVpnPilot.Server.Contracts.Requests;
using OpenVpnPilot.Server.Contracts.Responses;
using OpenVpnPilot.Server.Data.Entities;
using OpenVpnPilot.Server.Mapping;
using OpenVpnPilot.Server.Repositories.Interfaces;

namespace OpenVpnPilot.Server.Services.Profiles;

public interface IProfileImportService
{
    public Task<ProfileResponse> CreateAsync(ProfileCreateRequest request, CancellationToken cancellationToken);

    public Task<ProfileBatchResponse> CreateBatchAsync(ProfileBatchRequest request, CancellationToken cancellationToken);
}

public sealed class ProfileImportService(
    IProfileRepository profiles,
    IUnitOfWork unitOfWork,
    IProfileFactory factory,
    ITagAssigner tagAssigner,
    ICurrentUser currentUser,
    ILogger<ProfileImportService> logger) : IProfileImportService
{
    public async Task<ProfileResponse> CreateAsync(ProfileCreateRequest request, CancellationToken cancellationToken)
    {
        ProfileDraft draft = ProfileFactory.Examine(request);

        await using ISyncedWrite write = await unitOfWork.BeginSyncedWriteAsync(cancellationToken);
        string? existing = await profiles.FindNameByHashAsync(draft.Hash, null, cancellationToken);
        if (existing is not null)
        {
            throw ServiceException.Conflict(ErrorCodes.ProfileDuplicate, $"The profile '{existing}' has exactly this configuration.");
        }

        Profile profile = await BuildAsync(draft, write.ChangeSeq, cancellationToken);
        await write.CommitAsync(cancellationToken);
        ProfileLog.Created(logger, currentUser.Username, profile.Name, profile.Id, write.ChangeSeq);
        return profile.ToResponse();
    }

    // Every item is judged on its own, and all accepted ones are stored together in one transaction.
    public async Task<ProfileBatchResponse> CreateBatchAsync(ProfileBatchRequest request, CancellationToken cancellationToken)
    {
        if (request.Items is not { Count: > 0 and <= ProfileLimits.BatchItems })
        {
            throw ServiceException.Invalid("items", $"A batch holds 1 to {ProfileLimits.BatchItems} profiles.");
        }

        await using ISyncedWrite write = await unitOfWork.BeginSyncedWriteAsync(cancellationToken);
        HashSet<string> seen = new(StringComparer.Ordinal);
        List<(int Index, Profile? Profile, string? Code, string? Detail)> outcomes = [];

        for (int index = 0; index < request.Items.Count; index++)
        {
            try
            {
                ProfileDraft draft = ProfileFactory.Examine(request.Items[index]);
                string? existing = seen.Contains(draft.Hash)
                    ? "an earlier item of this batch"
                    : await profiles.FindNameByHashAsync(draft.Hash, null, cancellationToken);

                if (existing is not null)
                {
                    outcomes.Add((index, null, ErrorCodes.ProfileDuplicate, $"Identical to {Quote(existing)}."));
                    continue;
                }

                // Only an item that is actually created makes a later identical one a duplicate.
                outcomes.Add((index, await BuildAsync(draft, write.ChangeSeq, cancellationToken), null, null));
                seen.Add(draft.Hash);
            }
            catch (ServiceException refusal) when (refusal.Status == StatusCodes.Status400BadRequest)
            {
                outcomes.Add((index, null, refusal.Code, refusal.Message));
            }
        }

        await write.CommitAsync(cancellationToken);
        foreach ((int _, Profile? profile, string? _, string? _) in outcomes)
        {
            if (profile is not null)
            {
                ProfileLog.Created(logger, currentUser.Username, profile.Name, profile.Id, write.ChangeSeq);
            }
        }

        return Summarise(outcomes, request.Items.Count);
    }

    private async Task<Profile> BuildAsync(ProfileDraft draft, long changeSeq, CancellationToken cancellationToken)
    {
        Profile profile = factory.Create(draft, changeSeq);
        profile.Tags = await tagAssigner.ResolveAsync(draft.Request.Tags, changeSeq, cancellationToken);
        profiles.Add(profile);
        return profile;
    }

    private ProfileBatchResponse Summarise(List<(int Index, Profile? Profile, string? Code, string? Detail)> outcomes, int total)
    {
        List<ProfileBatchItemResponse> items = [.. outcomes.Select(o => new ProfileBatchItemResponse(
            o.Index,
            o.Profile is not null ? "created" : o.Code == ErrorCodes.ProfileDuplicate ? "duplicate" : "rejected",
            o.Profile?.ToResponse(),
            o.Code,
            o.Detail))];

        int created = items.Count(i => i.Outcome == "created");
        int duplicates = items.Count(i => i.Outcome == "duplicate");
        ProfileLog.BatchImported(logger, currentUser.Username, total, created, duplicates, total - created - duplicates);
        return new ProfileBatchResponse(created, duplicates, total - created - duplicates, items);
    }

    private static string Quote(string name) => name.StartsWith("an earlier", StringComparison.Ordinal) ? name : $"the profile '{name}'";
}
