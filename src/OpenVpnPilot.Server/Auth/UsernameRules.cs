using OpenVpnPilot.Server.Contracts;
using OpenVpnPilot.Server.Services;

namespace OpenVpnPilot.Server.Auth;

public static class UsernameRules
{
    public const int MaximumLength = 256;

    // Trims and checks a name before any provider sees it, so every mode stores names the same way.
    public static string Normalise(string? username)
    {
        string trimmed = username?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || trimmed.Length > MaximumLength || trimmed.Any(char.IsControl))
        {
            throw ServiceException.Unauthorized(ErrorCodes.InvalidCredentials, "The user name or password is not correct.");
        }

        return trimmed;
    }
}
