using System.Net.Security;
using OpenVpnPilot.Server.Security;

namespace OpenVpnPilot.Server.Commands;

// The two things the container needs besides serving: hashing a password for users.yaml, and a health
// probe that works without curl, which the runtime image does not have.
public static class CommandLine
{
    public static bool TryRun(string[] args, out int exitCode)
    {
        exitCode = 0;
        switch (args.FirstOrDefault())
        {
            case "hash-password":
                exitCode = HashPassword();
                return true;
            case "healthcheck":
                exitCode = HealthCheck(args.Skip(1).FirstOrDefault());
                return true;
            default:
                return false;
        }
    }

    private static int HashPassword()
    {
        // Read from standard input so the password never appears in the shell history or process list.
        Console.Error.Write("Password: ");
        string? password = Console.IsInputRedirected ? Console.In.ReadLine() : ReadHidden();
        Console.Error.WriteLine();
        if (string.IsNullOrEmpty(password))
        {
            Console.Error.WriteLine("No password given.");
            return 1;
        }

        Console.Out.WriteLine(new Argon2PasswordHasher().Hash(password));
        return 0;
    }

    private static string ReadHidden()
    {
        System.Text.StringBuilder builder = new();
        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                return builder.ToString();
            }

            if (key.Key == ConsoleKey.Backspace && builder.Length > 0)
            {
                builder.Length--;
            }
            else if (!char.IsControl(key.KeyChar))
            {
                builder.Append(key.KeyChar);
            }
        }
    }

    // Follows the same variables the server listens by, so the probe needs no configuration of its own.
    private static string DefaultHealthUrl()
    {
        bool proxy = string.Equals(Environment.GetEnvironmentVariable("OVP_TLS_MODE"), "proxy", StringComparison.OrdinalIgnoreCase);
        return proxy
            ? $"http://localhost:{Environment.GetEnvironmentVariable("OVP_HTTP_PORT") ?? "8080"}/health/live"
            : $"https://localhost:{Environment.GetEnvironmentVariable("OVP_HTTPS_PORT") ?? "8443"}/health/live";
    }

    private static int HealthCheck(string? url)
    {
        string target = url ?? DefaultHealthUrl();

        // Only ever pointed at this container itself, whose certificate is issued for a public name and
        // not for localhost, so the name check cannot succeed here and is not the point of the probe.
        using HttpClientHandler handler = new()
        {
            ServerCertificateCustomValidationCallback = (_, _, _, errors) =>
                errors is SslPolicyErrors.None or SslPolicyErrors.RemoteCertificateNameMismatch
                    or SslPolicyErrors.RemoteCertificateChainErrors
                    or (SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateChainErrors),
        };
        using HttpClient client = new(handler) { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            using HttpResponseMessage response = client.GetAsync(new Uri(target)).GetAwaiter().GetResult();
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (HttpRequestException exception)
        {
            Console.Error.WriteLine($"Health check of {target} failed: {exception.Message}");
            return 1;
        }
        catch (TaskCanceledException)
        {
            Console.Error.WriteLine($"Health check of {target} timed out.");
            return 1;
        }
    }
}
