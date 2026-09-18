using OpenVpnPilot.Server.Data.Entities;

namespace OpenVpnPilot.Server.Auth;

// Who an identity provider says someone is. Disabled is only set by a provider that proved the
// password first, so a wrong guess cannot learn that an account exists and is switched off.
public sealed record ExternalIdentity(
    string Username,
    string? DisplayName,
    string? ExternalId,
    UserRole Role,
    bool Disabled = false);

public enum AccountStatusKind
{
    Active,
    Disabled,
    Missing,
}

public sealed record ProviderAccountStatus(AccountStatusKind Kind, UserRole? Role = null)
{
    public static ProviderAccountStatus Active(UserRole role) => new(AccountStatusKind.Active, role);

    public static readonly ProviderAccountStatus Disabled = new(AccountStatusKind.Disabled);

    public static readonly ProviderAccountStatus Missing = new(AccountStatusKind.Missing);
}

public interface IAuthProvider
{
    public AuthProviderKind Kind { get; }

    // Proves a user name and password, or throws a ServiceException saying why not.
    public Task<ExternalIdentity> SignInAsync(string username, string? password, CancellationToken cancellationToken);

    // Asked on every token refresh: does the account still exist, is it allowed in, and in which role.
    public Task<ProviderAccountStatus> RecheckAsync(User user, CancellationToken cancellationToken);

    // Answers without any I/O where a provider can, so a removal takes effect on the very next request.
    // Null means the provider cannot tell cheaply and the next refresh will ask.
    public ProviderAccountStatus? QuickCheck(string username);
}
