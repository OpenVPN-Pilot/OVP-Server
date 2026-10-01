using OpenVpnPilot.Server.Configuration;
using OpenVpnPilot.Server.Data.Entities;
using OpenVpnPilot.Server.Security;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace OpenVpnPilot.Server.Auth.File;

// Reads users.yaml and reads it again when it changes. The modification time is compared rather than
// watched, because file events do not cross a bind mount from every host into a container. A changed
// file is only read once it was last written a moment ago, so a file caught halfway through being
// written never removes the users it has not reached yet.
public sealed class UserFileStore
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(2);

    private readonly AuthOptions options;
    private readonly IPasswordHasher hasher;
    private readonly TimeProvider time;
    private readonly ILogger<UserFileStore> logger;
    private readonly Lock gate = new();
    private Dictionary<string, UserFileUser> users = new(StringComparer.OrdinalIgnoreCase);
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

    public UserFileUser? Find(string username)
    {
        lock (gate)
        {
            DateTimeOffset now = time.GetUtcNow();
            if (now - lastCheck >= CheckInterval)
            {
                DateTime writeTime = System.IO.File.GetLastWriteTimeUtc(options.UserFilePath);
                if (writeTime == loadedWriteTime)
                {
                    lastCheck = now;
                }
                else if (now.UtcDateTime - writeTime >= SettleTime)
                {
                    lastCheck = now;
                    Load(throwOnError: false);
                }

                // Otherwise the file is being written right now; the next request looks again.
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
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or YamlException or InvalidDataException)
        {
            // Whoever edited the file wrongly still has everyone signed in; the log says what to fix.
            loadedWriteTime = System.IO.File.Exists(options.UserFilePath)
                ? System.IO.File.GetLastWriteTimeUtc(options.UserFilePath)
                : loadedWriteTime;
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

    private Dictionary<string, UserFileUser> Validate(UserFileDocument document)
    {
        // An empty list is far more often a file emptied by accident than a decision to lock everyone out,
        // and taking it at its word would tell every client to erase itself.
        if (document.Users is not { Count: > 0 })
        {
            throw new InvalidDataException("The file lists no users.");
        }

        Dictionary<string, UserFileUser> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (UserFileEntry? entry in document.Users)
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.Username) || string.IsNullOrEmpty(entry.Password))
            {
                throw new InvalidDataException("Every user needs a username and a password.");
            }

            string username = entry.Username.Trim();
            string role = string.IsNullOrWhiteSpace(entry.Role) ? "user" : entry.Role.Trim().ToLowerInvariant();
            if (role is not ("admin" or "user"))
            {
                throw new InvalidDataException($"The user '{username}' has role '{entry.Role}'; it must be admin or user.");
            }

            UserFileUser user = new(
                username, entry.Password, role == "admin" ? UserRole.Admin : UserRole.User, entry.DisplayName, entry.Disabled);
            if (!result.TryAdd(username, user))
            {
                throw new InvalidDataException($"The user '{username}' appears more than once.");
            }

            if (!hasher.IsHash(entry.Password))
            {
                AuthLog.PlainPasswordInFile(logger, username);
            }
        }

        return result;
    }
}
