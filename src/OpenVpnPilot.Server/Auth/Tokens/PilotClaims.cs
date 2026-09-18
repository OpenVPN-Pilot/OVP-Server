namespace OpenVpnPilot.Server.Auth.Tokens;

public static class PilotClaims
{
    public const string Issuer = "openvpnpilot-server";
    public const string Audience = "openvpnpilot-client";

    public const string Subject = "sub";
    public const string Name = "name";
    public const string Role = "role";
    public const string SecurityStamp = "stamp";
    public const string ClientId = "cid";

    public const string AdminRole = "admin";
    public const string UserRole = "user";
}
