using System.ComponentModel.DataAnnotations;

namespace OpenVpnPilot.Server.Contracts.Requests;

/// <summary>A shared sign in for one profile and realm.</summary>
/// <param name="Username">The user name, for realm <c>Auth</c>. Null for a private key passphrase.</param>
/// <param name="Password">The password or passphrase.</param>
public sealed record VaultEntryRequest(
    [MaxLength(512)] string? Username,
    [Required, MaxLength(4096)] string Password);
