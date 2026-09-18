using System.Globalization;

namespace OpenVpnPilot.Server.Configuration;

// Collects every problem with the environment before anything fails, so an operator fixes all of them
// in one pass instead of discovering them one restart at a time.
public sealed class EnvironmentReader(IConfiguration configuration)
{
    private readonly List<string> errors = [];

    public IReadOnlyList<string> Errors => errors;

    public string? Optional(string name)
    {
        string? value = configuration[name];
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public string Text(string name, string fallback) => Optional(name) ?? fallback;

    public string Required(string name)
    {
        string? value = Optional(name);
        if (value is null)
        {
            Fail(name, "is required and not set.");
            return string.Empty;
        }

        return value;
    }

    public int WholeNumber(string name, int fallback, int minimum, int maximum)
    {
        string? value = Optional(name);
        if (value is null)
        {
            return fallback;
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed)
            || parsed < minimum || parsed > maximum)
        {
            Fail(name, $"must be a whole number from {minimum} to {maximum}, was '{value}'.");
            return fallback;
        }

        return parsed;
    }

    public bool Switch(string name, bool fallback)
    {
        string? value = Optional(name);
        switch (value?.ToLowerInvariant())
        {
            case null:
                return fallback;
            case "true" or "1" or "yes" or "on":
                return true;
            case "false" or "0" or "no" or "off":
                return false;
            default:
                Fail(name, $"must be true or false, was '{value}'.");
                return fallback;
        }
    }

    public T Choice<T>(string name, T? fallback) where T : struct, Enum
    {
        string? value = Optional(name);
        if (value is null)
        {
            if (fallback is null)
            {
                Fail(name, $"is required, one of: {Allowed<T>()}.");
                return default;
            }

            return fallback.Value;
        }

        // Enum.TryParse also accepts numbers, which would let '7' through as a mode nobody defined.
        if (value.All(char.IsAsciiDigit) || !Enum.TryParse(value, true, out T parsed))
        {
            Fail(name, $"must be one of: {Allowed<T>()}, was '{value}'.");
            return fallback ?? default;
        }

        return parsed;
    }

    public IReadOnlyList<string> List(string name)
    {
        string? value = Optional(name);
        return value is null
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public byte[] Key(string name, int minimumBytes, int? exactBytes = null)
    {
        string value = Required(name);
        if (value.Length == 0)
        {
            return [];
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            Fail(name, "must be Base64. Generate one with: openssl rand -base64 32");
            return [];
        }

        if (key.Length < minimumBytes || (exactBytes is not null && key.Length != exactBytes))
        {
            string expected = exactBytes is null ? $"at least {minimumBytes}" : $"exactly {exactBytes}";
            Fail(name, $"must decode to {expected} bytes, decodes to {key.Length}.");
            return [];
        }

        return key;
    }

    public void Fail(string name, string message) => errors.Add($"{name} {message}");

    private static string Allowed<T>() where T : struct, Enum =>
        string.Join(", ", Enum.GetNames<T>().Select(n => n.ToLowerInvariant()));
}
