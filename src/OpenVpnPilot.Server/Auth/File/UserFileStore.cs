using OpenVpnPilot.Server.Configuration;
using OpenVpnPilot.Server.Security;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace OpenVpnPilot.Server.Auth.File;

// Reads users.yaml and reads it again when it changes. The modification time is compared rather than
// watched, because file events do not cross a bind mount from every host into a container.
public sealed class UserFileStore
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);

    private readonly AuthOptions options;
    private readonly IPasswordHasher hasher;
    private readonly TimeProvider time;
    private readonly ILogger<UserFileStore> logger;
    private readonly Lock gate = new();
    private Dictionary<string, UserFileEntry> users = new(StringComparer.OrdinalIgnoreCase);
    private DateTime loadedWriteTime;
    private DateTimeOffset lastCheck = DateTimeOffset.MinValue;

    public UserFileStore(AuthOptions options, IPasswordHasher hasher, TimeProvider time, ILogger<UserFileStore> logger)
    {
        this.options = options;
        this.hasher = hasher;
        this.time = time;
        this.logger = logger;

        // A broken file at start is a configuration error; a broken file later keeps the last good one.
        Load(throwOnError: true);
    }

    public UserFileEntry? Find(string username)
    {
        lock (gate)
        {
            DateTimeOffset now = time.GetUtcNow();
            if (now - lastCheck >= CheckInterval)
            {
                lastCheck = now;
                if (System.IO.File.GetLastWriteTimeUtc(options.UserFilePath) != loadedWriteTime)
                {
                    Load(throwOnError: false);
                }
            }

            return users.GetValueOrDefault(username);
        }
    }

    private void Load(bool throwOnError)
    {
        try
        {
            DateTime writeTime = System.IO.File.GetLastWriteTimeUtc(options.UserFilePath);
            UserFileDocument document = Parse(System.IO.File.ReadAllText(options.UserFilePath));
            users = Validate(document);
            loadedWriteTime = writeTime;
            AuthLog.UserFileLoaded(logger, options.UserFilePath, users.Count);
        }
        catch (Exception exception) when (exception is IOException or YamlException or InvalidDataException)
        {
            if (throwOnError)
            {
                throw new ConfigurationException([$"OVP_AUTH_FILE could not be read: {exception.Message}"]);
            }

            AuthLog.UserFileRejected(logger, options.UserFilePath, exception);
        }
    }

    private static UserFileDocument Parse(string yaml) =>
        new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build()
            .Deserialize<UserFileDocument?>(yaml) ?? new UserFileDocument();

    private Dictionary<string, UserFileEntry> Validate(UserFileDocument document)
    {
        Dictionary<string, UserFileEntry> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (UserFileEntry entry in document.Users)
        {
            entry.Username = entry.Username.Trim();
            if (entry.Username.Length == 0 || entry.Password.Length == 0)
            {
                throw new InvalidDataException("Every user needs a username and a password.");
            }

            if (!result.TryAdd(entry.Username, entry))
            {
                throw new InvalidDataException($"The user '{entry.Username}' appears more than once.");
            }

            if (entry.Role.ToLowerInvariant() is not ("admin" or "user"))
            {
                throw new InvalidDataException($"The user '{entry.Username}' has role '{entry.Role}'; it must be admin or user.");
            }

            if (!hasher.IsHash(entry.Password))
            {
                AuthLog.PlainPasswordInFile(logger, entry.Username);
            }
        }

        return result;
    }
}
