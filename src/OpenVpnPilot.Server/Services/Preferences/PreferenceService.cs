using OpenVpnPilot.Server.Auth;
using OpenVpnPilot.Server.Contracts;
using OpenVpnPilot.Server.Contracts.Requests;
using OpenVpnPilot.Server.Contracts.Responses;
using OpenVpnPilot.Server.Data.Entities;
using OpenVpnPilot.Server.Repositories.Interfaces;

namespace OpenVpnPilot.Server.Services.Preferences;

public interface IPreferenceService
{
    public Task<FavouritesResponse> FavouritesAsync(CancellationToken cancellationToken);

    public Task<FavouritesResponse> ReplaceFavouritesAsync(FavouritesRequest request, CancellationToken cancellationToken);

    public Task<HotkeysResponse> HotkeysAsync(CancellationToken cancellationToken);

    public Task<HotkeysResponse> ReplaceHotkeysAsync(HotkeysRequest request, CancellationToken cancellationToken);
}

// Favourites and shortcuts belong to one person, not to the team, and follow them from machine to machine.
public sealed class PreferenceService(
    IUserPreferenceRepository preferences,
    IProfileRepository profiles,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    ILogger<PreferenceService> logger) : IPreferenceService
{
    public async Task<FavouritesResponse> FavouritesAsync(CancellationToken cancellationToken) =>
        new([.. (await preferences.FavouritesAsync(currentUser.Id, cancellationToken)).Select(f => new FavouriteItem(f.ProfileId, f.Slot))]);

    public async Task<FavouritesResponse> ReplaceFavouritesAsync(FavouritesRequest request, CancellationToken cancellationToken)
    {
        if (request.Items.Select(i => i.ProfileId).Distinct().Count() != request.Items.Count)
        {
            throw ServiceException.Invalid("items", "A profile appears more than once.");
        }

        if (request.Items.Where(i => i.Slot is not null).GroupBy(i => i.Slot).Any(g => g.Count() > 1))
        {
            throw ServiceException.Invalid("items", "A slot is used more than once.");
        }

        await RequireProfilesAsync(request.Items.Select(i => i.ProfileId), cancellationToken);

        await using IWriteTransaction transaction = await unitOfWork.BeginAsync(cancellationToken);
        await preferences.ReplaceFavouritesAsync(
            currentUser.Id,
            [.. request.Items.Select(i => new UserFavourite { UserId = currentUser.Id, ProfileId = i.ProfileId, Slot = i.Slot })],
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        PreferenceLog.FavouritesStored(logger, currentUser.Username, request.Items.Count);
        return await FavouritesAsync(cancellationToken);
    }

    public async Task<HotkeysResponse> HotkeysAsync(CancellationToken cancellationToken) =>
        new([.. (await preferences.HotkeysAsync(currentUser.Id, cancellationToken))
            .Select(h => new HotkeyItem(h.ActionId, h.Gesture, h.ProfileId, h.IsEnabled))]);

    public async Task<HotkeysResponse> ReplaceHotkeysAsync(HotkeysRequest request, CancellationToken cancellationToken)
    {
        if (request.Items.Select(i => i.ActionId).Distinct(StringComparer.Ordinal).Count() != request.Items.Count)
        {
            throw ServiceException.Invalid("items", "An action appears more than once.");
        }

        await RequireProfilesAsync(request.Items.Where(i => i.ProfileId is not null).Select(i => i.ProfileId!.Value), cancellationToken);

        await using IWriteTransaction transaction = await unitOfWork.BeginAsync(cancellationToken);
        await preferences.ReplaceHotkeysAsync(
            currentUser.Id,
            [.. request.Items.Select(i => new UserHotkey
            {
                UserId = currentUser.Id,
                ActionId = i.ActionId,
                Gesture = i.Gesture,
                ProfileId = i.ProfileId,
                IsEnabled = i.IsEnabled,
            })],
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        PreferenceLog.HotkeysStored(logger, currentUser.Username, request.Items.Count);
        return await HotkeysAsync(cancellationToken);
    }

    private async Task RequireProfilesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        List<Guid> wanted = [.. ids.Distinct()];
        IReadOnlySet<Guid> existing = await profiles.ExistingIdsAsync(wanted, cancellationToken);
        Guid? missing = wanted.FirstOrDefault(id => !existing.Contains(id));
        if (wanted.Count > existing.Count)
        {
            throw ServiceException.NotFound(ErrorCodes.ProfileNotFound, $"There is no profile {missing}.");
        }
    }
}
