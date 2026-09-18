namespace OpenVpnPilot.Server.Data.Entities;

// A single row. Once tombstones are pruned, a cursor older than the newest pruned one can no longer
// be answered with a delta and the client has to start over.
public sealed class SyncState
{
    public int Id { get; set; }

    public long PrunedThrough { get; set; }
}
