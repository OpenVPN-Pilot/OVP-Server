using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenVpnPilot.Server.Configuration;
using OpenVpnPilot.Server.Data.Entities;

namespace OpenVpnPilot.Server.Auth.Tokens;

public sealed record IssuedAccessToken(string Token, DateTimeOffset ExpiresAt);

public sealed class AccessTokenIssuer(SecurityOptions options, TimeProvider time)
{
    private readonly SymmetricSecurityKey key = new(options.JwtSigningKey);

    public SymmetricSecurityKey SigningKey => key;

    // Carries the security stamp so every token of a user stops working the moment the stamp changes,
    // and the client id so a token copied to another installation is refused there.
    public IssuedAccessToken Issue(User user, Guid clientId)
    {
        DateTimeOffset now = time.GetUtcNow();
        DateTimeOffset expires = now + options.AccessTokenLifetime;
        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = PilotClaims.Issuer,
            Audience = PilotClaims.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim(PilotClaims.Subject, user.Id.ToString()),
                new Claim(PilotClaims.Name, user.Username),
                new Claim(PilotClaims.Role, user.Role == UserRole.Admin ? PilotClaims.AdminRole : PilotClaims.UserRole),
                new Claim(PilotClaims.SecurityStamp, user.SecurityStamp.ToString()),
                new Claim(PilotClaims.ClientId, clientId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ]),
        };

        return new IssuedAccessToken(new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }
}
