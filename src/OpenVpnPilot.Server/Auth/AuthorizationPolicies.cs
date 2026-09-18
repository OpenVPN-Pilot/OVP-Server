namespace OpenVpnPilot.Server.Auth;

public static class AuthorizationPolicies
{
    // Anything that changes what the whole team sees, and managing who may see it.
    public const string Admin = "admin";

    // Sign in and refresh are the only anonymous endpoints worth guessing against.
    public const string SignInRateLimit = "sign-in";
}
