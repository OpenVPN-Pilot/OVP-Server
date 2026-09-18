using Npgsql;

namespace OpenVpnPilot.Server.Configuration;

public sealed record DatabaseOptions
{
    public required string ConnectionString { get; init; }

    public required bool MigrateOnStart { get; init; }

    public static DatabaseOptions Read(EnvironmentReader env)
    {
        NpgsqlConnectionStringBuilder builder = new()
        {
            Host = env.Text("OVP_DB_HOST", "postgres"),
            Port = env.WholeNumber("OVP_DB_PORT", 5432, 1, 65535),
            Database = env.Text("OVP_DB_NAME", "ovp"),
            Username = env.Text("OVP_DB_USER", "ovp"),
            Password = env.Required("OVP_DB_PASSWORD"),

            // Npgsql otherwise probes for Kerberos first, which the runtime image does not carry, and
            // prints a library error on every start that looks like a failure and is not one.
            GssEncryptionMode = GssEncryptionMode.Disable,
        };

        return new DatabaseOptions
        {
            ConnectionString = builder.ConnectionString,
            MigrateOnStart = env.Switch("OVP_DB_MIGRATE_ON_START", true),
        };
    }
}
