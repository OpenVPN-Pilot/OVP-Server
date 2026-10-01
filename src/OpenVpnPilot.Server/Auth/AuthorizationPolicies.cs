namespace OpenVpnPilot.Server.Auth;

public static class AuthorizationPolicies
{
    // Anything that changes what the whole team sees, and managing who may see it.
    public const string Admin = "admin";

    // Sign in is the only anonymous endpoint worth guessing passwords against.
    public const string SignInRateLimit = "sign-in";

    // A refresh token cannot be guessed, so this only stops a runaway client. It counts per installation,
    // not per address, because a whole office behind one address refreshes at the same moments.
    public const string RefreshRateLimit = "refresh";

    public const int RefreshesPerMinute = 30;
}
