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
        };

        return new DatabaseOptions
        {
            ConnectionString = builder.ConnectionString,
            MigrateOnStart = env.Switch("OVP_DB_MIGRATE_ON_START", true),
        };
    }
}
