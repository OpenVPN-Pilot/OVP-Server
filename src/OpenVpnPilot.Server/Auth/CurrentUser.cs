using System.Security.Claims;
using OpenVpnPilot.Server.Auth.Tokens;

namespace OpenVpnPilot.Server.Auth;

public interface ICurrentUser
{
    public Guid Id { get; }

    public string Username { get; }

    public bool IsAdmin { get; }
}

// Reads the caller from the validated access token. Only used behind authorisation, so a missing
// claim is a defect rather than a request to refuse.
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal Principal =>
        accessor.HttpContext?.User ?? throw new InvalidOperationException("No request is in progress.");

    public Guid Id => Guid.Parse(Claim(PilotClaims.Subject));

    public string Username => Claim(PilotClaims.Name);

    public bool IsAdmin => Principal.HasClaim(PilotClaims.Role, PilotClaims.AdminRole);

    private string Claim(string type) =>
        Principal.FindFirst(type)?.Value ?? throw new InvalidOperationException($"The access token carries no '{type}' claim.");
}
