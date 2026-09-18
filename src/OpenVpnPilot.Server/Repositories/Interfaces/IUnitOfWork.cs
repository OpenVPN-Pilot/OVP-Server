namespace OpenVpnPilot.Server.Repositories.Interfaces;

public interface IUnitOfWork
{
    // Writes that clients synchronise run one at a time, so change numbers are committed in the order
    // they were handed out and a cursor never skips a change that committed late.
    public Task<ISyncedWrite> BeginSyncedWriteAsync(CancellationToken cancellationToken);

    // Reads that produce a cursor wait for any synchronised write in progress and see none half done.
    public Task<ISyncedRead> BeginSyncedReadAsync(CancellationToken cancellationToken);

    // A plain transaction for writes that are not synchronised but must happen together.
    public Task<IWriteTransaction> BeginAsync(CancellationToken cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IWriteTransaction : IAsyncDisposable
{
    // Saves pending changes and commits. Disposing without committing rolls everything back.
    public Task CommitAsync(CancellationToken cancellationToken);
}

public interface ISyncedWrite : IWriteTransaction
{
    public long ChangeSeq { get; }
}

public interface ISyncedRead : IAsyncDisposable
{
    // The highest change number handed out so far. Every change up to it is committed and visible.
    public long Cursor { get; }
}
