using OpenVpnPilot.Server.Auth;
using OpenVpnPilot.Server.Contracts.Requests;
using OpenVpnPilot.Server.Data.Entities;
using OpenVpnPilot.Server.Security;

namespace OpenVpnPilot.Server.Services.Profiles;

// Builds and rewrites profile entities from requests, so creating one and creating five hundred
// follow exactly the same rules.
public sealed class ProfileFactory(ISecretCipher cipher, ICurrentUser currentUser, TimeProvider time)
{
    public static string CipherContext(Guid profileId) => "profile:" + profileId.ToString("N");

    // Checks a request without touching the database; throws what a client would be told.
    public static (OvpnFacts Facts, string Hash) Examine(ProfileCreateRequest request)
    {
        RequireName(request.Name);
        OvpnFacts facts = OvpnInspector.Inspect(request.Configuration);
        return (facts, TokenHashing.ContentHash(request.Configuration));
    }

    public Profile Create(ProfileCreateRequest request, OvpnFacts facts, string hash, long changeSeq)
    {
        DateTimeOffset now = time.GetUtcNow();
        Guid id = Guid.CreateVersion7();
        Profile profile = new()
        {
            Id = id,
            Name = request.Name.Trim(),
            Notes = request.Notes,
            Colour = request.Colour,
            ProtectRoutes = request.ProtectRoutes,
            ChangeSeq = changeSeq,
            CreatedAt = now,
            CreatedBy = currentUser.Username,
            UpdatedAt = now,
            UpdatedBy = currentUser.Username,
            Content = new ProfileContent { ProfileId = id },
        };
        ApplyConfiguration(profile, request.Configuration, facts, hash);
        return profile;
    }

    public void ApplyConfiguration(Profile profile, string configuration, OvpnFacts facts, string hash)
    {
        profile.ContentHash = hash;
        profile.RemoteHost = facts.RemoteHost;
        profile.RemotePort = facts.RemotePort;
        profile.Protocol = facts.Protocol;
        profile.RequiresCredentials = facts.RequiresCredentials;
        profile.HasUnsupportedOptions = facts.HasUnsupportedOptions;
        profile.Content ??= new ProfileContent { ProfileId = profile.Id };
        profile.Content.Cipher = cipher.Encrypt(configuration, CipherContext(profile.Id));
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

        // A line break in a name would forge lines in the log and in every client's list.
        if (name.Any(char.IsControl))
        {
            throw ServiceException.Invalid("name", "A profile name cannot contain control characters.");
        }
    }
}
