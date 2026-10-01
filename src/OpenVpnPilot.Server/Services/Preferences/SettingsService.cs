using System.Text.Json;
using OpenVpnPilot.Server.Auth;
using OpenVpnPilot.Server.Contracts.Requests;
using OpenVpnPilot.Server.Contracts.Responses;
using OpenVpnPilot.Server.Data.Entities;
using OpenVpnPilot.Server.Mapping;
using OpenVpnPilot.Server.Repositories.Interfaces;

namespace OpenVpnPilot.Server.Services.Preferences;

public interface ISettingsService
{
    public Task<SettingsResponse> GetAsync(CancellationToken cancellationToken);

    public Task<SettingsResponse> ReplaceAsync(SettingsRequest request, IfMatch? precondition, CancellationToken cancellationToken);
}

public sealed class SettingsService(
    IUserPreferenceRepository preferences,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider time,
    ILogger<SettingsService> logger) : ISettingsService
{
    public const int MaximumBytes = 64 * 1024;

    private static readonly JsonElement EmptyDocument = JsonDocument.Parse("{}").RootElement.Clone();

    public async Task<SettingsResponse> GetAsync(CancellationToken cancellationToken)
    {
        UserSettingsDocument? stored = await preferences.SettingsAsync(currentUser.Id, cancellationToken);
        return stored is null ? new SettingsResponse(0, EmptyDocument, null, null) : ToResponse(stored);
    }

    // If-Match is optional here: settings belong to one person, and a client that does not care which
    // of its own machines wrote last may simply overwrite.
    public async Task<SettingsResponse> ReplaceAsync(SettingsRequest request, IfMatch? precondition, CancellationToken cancellationToken)
    {
        if (request.Document.ValueKind != JsonValueKind.Object)
        {
            throw ServiceException.Invalid("document", "The settings document must be a JSON object.");
        }

        string json = request.Document.GetRawText();
        int bytes = System.Text.Encoding.UTF8.GetByteCount(json);
        if (bytes > MaximumBytes)
        {
            throw ServiceException.Invalid("document", $"The settings document is larger than {MaximumBytes / 1024} KiB.");
        }

        UserSettingsDocument? stored = await preferences.SettingsAsync(currentUser.Id, cancellationToken);
        if (stored is null)
        {
            if (precondition is not null)
            {
                throw ServiceException.PreconditionFailed("No settings are stored yet, so there is no version to match.");
            }

            stored = new UserSettingsDocument { UserId = currentUser.Id };
            preferences.AddSettings(stored);
        }
        else if (precondition is not null)
        {
            if (!precondition.Matches(stored.Version))
            {
                throw ServiceException.PreconditionFailed("Another machine stored settings since they were read. Read them again.");
            }

            preferences.ExpectSettingsVersion(stored, stored.Version);
        }

        stored.SchemaVersion = request.SchemaVersion;
        stored.Document = json;
        stored.UpdatedAt = time.GetUtcNow();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        PreferenceLog.SettingsStored(logger, currentUser.Username, request.SchemaVersion, bytes);
        return ToResponse(stored);
    }

    private static SettingsResponse ToResponse(UserSettingsDocument stored)
    {
        using JsonDocument document = JsonDocument.Parse(stored.Document);
        return new SettingsResponse(stored.SchemaVersion, document.RootElement.Clone(), ETags.Format(stored.Version), stored.UpdatedAt);
    }
}
