namespace OpenVpnPilot.Server.Contracts.Responses;

/// <summary>A shared sign in. Store it in the operating system keystore under the profile and realm, never in a file.</summary>
/// <param name="ProfileId">The profile it belongs to.</param>
/// <param name="Realm">OpenVPN's realm: <c>Auth</c> for user name and password, or the name of a private key for its passphrase.</param>
/// <param name="Username">The user name, when the realm has one.</param>
/// <param name="Password">The password or passphrase, in plain text. It only ever travels inside TLS.</param>
/// <param name="ChangeSeq">The change number of the last change.</param>
/// <param name="CreatedAt">When it was added.</param>
/// <param name="CreatedBy">Who added it.</param>
/// <param name="UpdatedAt">When it last changed.</param>
/// <param name="UpdatedBy">Who changed it last.</param>
public sealed record VaultEntryResponse(
    Guid ProfileId,
    string Realm,
    string? Username,
    string Password,
    long ChangeSeq,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset UpdatedAt,
    string UpdatedBy);
