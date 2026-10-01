using System.Text.RegularExpressions;
using OpenVpnPilot.Server.Auth;
using OpenVpnPilot.Server.Contracts.Requests;
using OpenVpnPilot.Server.Data.Entities;
using OpenVpnPilot.Server.Security;

namespace OpenVpnPilot.Server.Services.Profiles;

// A request that passed every check that needs no database, with its configuration as it will be stored.
public sealed record ProfileDraft(ProfileCreateRequest Request, OvpnInspection Inspection, string Hash);

public interface IProfileFactory
{
    public Profile Create(ProfileDraft draft, long changeSeq);

    public void ApplyConfiguration(Profile profile, OvpnInspection inspection, string hash);

    public void Touch(Profile profile, long changeSeq);

    public string ReadConfiguration(Profile profile);
}

// Builds and rewrites profile entities from requests, so creating one and creating five hundred
// follow exactly the same rules.
public sealed partial class ProfileFactory(ISecretCipher cipher, ICurrentUser currentUser, TimeProvider time) : IProfileFactory
{
    public static string CipherContext(Guid profileId) => "profile:" + profileId.ToString("N");

    // Checks a request without touching the database; throws what a client would be told. The batch
    // relies on this for every limit, because MVC does not validate its items.
    public static ProfileDraft Examine(ProfileCreateRequest? request)
    {
        if (request is null)
        {
            throw ServiceException.Invalid("item", "An item of the batch is null.");
        }

        RequireName(request.Name);
        RequireLength("notes", request.Notes, ProfileLimits.Notes);
        if (request.Colour is not null && !ColourPattern().IsMatch(request.Colour))
        {
            throw ServiceException.Invalid("colour", "A colour is #RRGGBB or #RRGGBBAA.");
        }

        TagAssigner.Normalise(request.Tags);
        if (request.Configuration is null)
        {
            throw ServiceException.Invalid("configuration", "A profile needs a configuration.");
        }

        OvpnInspection inspection = OvpnInspector.Inspect(request.Configuration);
        return new ProfileDraft(request, inspection, TokenHashing.ContentHash(inspection.Configuration));
    }

    public Profile Create(ProfileDraft draft, long changeSeq)
    {
        DateTimeOffset now = time.GetUtcNow();
        Guid id = Guid.CreateVersion7();
        Profile profile = new()
        {
            Id = id,
            Name = draft.Request.Name.Trim(),
            Notes = draft.Request.Notes,
            Colour = draft.Request.Colour,
            ProtectRoutes = draft.Request.ProtectRoutes,
            ChangeSeq = changeSeq,
            CreatedAt = now,
            CreatedBy = currentUser.Username,
            UpdatedAt = now,
            UpdatedBy = currentUser.Username,
            Content = new ProfileContent { ProfileId = id },
        };
        ApplyConfiguration(profile, draft.Inspection, draft.Hash);
        return profile;
    }

    public void ApplyConfiguration(Profile profile, OvpnInspection inspection, string hash)
    {
        OvpnFacts facts = inspection.Facts;
        profile.ContentHash = hash;
        profile.RemoteHost = facts.RemoteHost;
        profile.RemotePort = facts.RemotePort;
        profile.Protocol = facts.Protocol;
        profile.RequiresCredentials = facts.RequiresCredentials;
        profile.HasUnsupportedOptions = facts.HasUnsupportedOptions;
        profile.Content ??= new ProfileContent { ProfileId = profile.Id };
        profile.Content.Cipher = cipher.Encrypt(inspection.Configuration, CipherContext(profile.Id));
    }

    public void Touch(Profile profile, long changeSeq)
    {
        profile.ChangeSeq = changeSeq;
        profile.UpdatedAt = time.GetUtcNow();
        profile.UpdatedBy = currentUser.Username;
    }

    public string ReadConfiguration(Profile profile) =>
        cipher.Decrypt(profile.Content!.Cipher, CipherContext(profile.Id));

    public static void RequireName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw ServiceException.Invalid("name", "A profile needs a name.");
        }

        RequireLength("name", name, ProfileLimits.Name);

        // A line break in a name would forge lines in the log and in every client's list.
        if (name.Any(char.IsControl))
        {
            throw ServiceException.Invalid("name", "A profile name cannot contain control characters.");
        }
    }

    private static void RequireLength(string field, string? value, int maximum)
    {
        if (value?.Length > maximum)
        {
            throw ServiceException.Invalid(field, $"The {field} is at most {maximum} characters.");
        }
    }

    [GeneratedRegex(ContractPatterns.Colour)]
    private static partial Regex ColourPattern();
}
